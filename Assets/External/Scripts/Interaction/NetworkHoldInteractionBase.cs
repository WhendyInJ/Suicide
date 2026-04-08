using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.UI;

public enum HoldInteractionStopReason
{
    None = 0,
    Cancelled = 1,
    Completed = 2
}

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(PhotonView))]
public abstract class NetworkHoldInteractionBase : MonoBehaviourPunCallbacks
{
    [Header("References")]
    [SerializeField] private Canvas interactionCanvas;
    [SerializeField] private Image holdProgressImage;

    [Header("Interaction")]
    [SerializeField, Min(0.01f)] private float holdDuration = 1f;
    [SerializeField, Min(0.01f)] private float cancelReturnDuration = 0.2f;
    [SerializeField] private bool lockPlayerMovementWhileInteracting = true;
    [SerializeField, Min(0f)] private float interactionLockBuffer = 0.25f;
    [SerializeField] private int interactionLockPriority = 1000;

    [Header("Runtime Debug")]
    [SerializeField] private bool isSomeoneInteracting;
    [SerializeField] private int interactingPlayerViewId = -1;
    [SerializeField] private double interactionStartServerTime = -1d;
    [SerializeField] private double lastStateChangeServerTime = -1d;
    [SerializeField] private HoldInteractionStopReason lastStopReason = HoldInteractionStopReason.None;
    [SerializeField] private bool localRequestPending;

    private readonly HashSet<int> localOverlapColliderIds = new();

    private PlayerInputSource currentLocalInputSource;
    private PlayerController currentLocalPlayerController;
    private int currentLocalPlayerViewId = -1;
    private int activeMovementLockId = -1;

    private bool previousInteractionActive;
    private int previousInteractingPlayerViewId = -1;
    private double previousStateChangeServerTime = -1d;

    protected bool IsSomeoneInteracting => isSomeoneInteracting;
    /// <summary>True while a player is holding this interaction (state is replicated to all clients).</summary>
    public bool IsBeingHeld => isSomeoneInteracting;

    /// <summary>PhotonView.ViewID of the holding player, or -1 if nobody is holding.</summary>
    public int HolderPlayerViewId => interactingPlayerViewId;

    protected float HoldDuration => holdDuration;
    protected float CancelReturnDuration => cancelReturnDuration;
    protected int InteractingPlayerViewId => interactingPlayerViewId;
    protected double InteractionStartServerTime => interactionStartServerTime;
    protected double LastStateChangeServerTime => lastStateChangeServerTime;
    protected bool IsLocalPlayerInRange => localOverlapColliderIds.Count > 0;
    protected bool IsLocalInteractingPlayer => currentLocalPlayerViewId > 0 && currentLocalPlayerViewId == interactingPlayerViewId;
    protected double NetworkTime => PhotonNetwork.InRoom ? PhotonNetwork.Time : Time.timeAsDouble;
    protected bool CanExecuteAuthoritativeTrigger => !PhotonNetwork.InRoom || PhotonNetwork.IsMasterClient;

    protected virtual void Reset()
    {
        Collider trigger = GetComponent<Collider>();
        if (trigger != null)
            trigger.isTrigger = true;
    }

    protected virtual void Awake()
    {
        ApplyHoldProgressVisual(0f);
        UpdateCanvasVisibility();
    }

    protected virtual void Update()
    {
        TickMasterInteraction();
        HandleLocalInteractionInput();
        HandleReplicatedStateTransitions();
        UpdateInteractionPresentation();
    }

    public override void OnDisable()
    {
        base.OnDisable();

        localOverlapColliderIds.Clear();
        localRequestPending = false;

        ReleaseLocalMovementLock();
        ClearLocalInteractionContext();
        ApplyHoldProgressVisual(0f);
        ApplyIdleVisualImmediate();
        UpdateCanvasVisibility();
    }

    protected virtual void OnTriggerEnter(Collider other)
    {
        TryEnterRange(other);
    }

    protected virtual void OnTriggerStay(Collider other)
    {
        TryEnterRange(other);
    }

    protected virtual void OnTriggerExit(Collider other)
    {
        if (!TryResolveLocalPlayer(other, out int colliderId, out _, out _))
            return;

        if (!localOverlapColliderIds.Remove(colliderId))
            return;

        if (localOverlapColliderIds.Count > 0)
            return;

        if (IsLocalInteractingPlayer || localRequestPending)
        {
            RequestCancelInteraction();
            UpdateCanvasVisibility();
            return;
        }

        ClearLocalInteractionContext();
        UpdateCanvasVisibility();
    }

    private void TryEnterRange(Collider other)
    {
        if (!TryResolveLocalPlayer(other, out int colliderId, out PlayerInputSource inputSource, out PlayerController playerController))
            return;

        localOverlapColliderIds.Add(colliderId);
        currentLocalInputSource = inputSource;
        currentLocalPlayerController = playerController;
        currentLocalPlayerViewId = ResolvePhotonViewId(inputSource);
        UpdateCanvasVisibility();
    }

    private bool TryResolveLocalPlayer(
        Collider other,
        out int colliderId,
        out PlayerInputSource inputSource,
        out PlayerController playerController)
    {
        colliderId = 0;
        inputSource = null;
        playerController = null;

        if (other == null)
            return false;

        inputSource = other.GetComponentInParent<PlayerInputSource>();
        if (inputSource == null || !inputSource.HasLocalAuthority)
            return false;

        playerController = other.GetComponentInParent<PlayerController>();
        if (playerController == null || !playerController.HasLocalAuthority)
            return false;

        colliderId = other.GetInstanceID();
        return true;
    }

    private void HandleLocalInteractionInput()
    {
        if (currentLocalInputSource == null || currentLocalPlayerViewId <= 0)
            return;

        if (!isSomeoneInteracting)
        {
            if (!localRequestPending && currentLocalInputSource.InteractPressedThisFrame)
            {
                RequestBeginInteraction();
            }

            return;
        }

        if (!IsLocalInteractingPlayer)
            return;

        if (!IsLocalPlayerInRange || !currentLocalInputSource.InteractHeld)
        {
            RequestCancelInteraction();
        }
    }

    private void RequestBeginInteraction()
    {
        if (currentLocalPlayerViewId <= 0)
            return;

        localRequestPending = true;

        if (!PhotonNetwork.InRoom)
        {
            BroadcastInteractionStarted(currentLocalPlayerViewId, NetworkTime);
            return;
        }

        photonView.RPC(
            nameof(RPC_RequestBeginInteraction),
            RpcTarget.MasterClient,
            currentLocalPlayerViewId);
    }

    private void RequestCancelInteraction()
    {
        if (currentLocalPlayerViewId <= 0)
            return;

        if (!PhotonNetwork.InRoom)
        {
            BroadcastInteractionStopped(currentLocalPlayerViewId, NetworkTime, HoldInteractionStopReason.Cancelled);
            return;
        }

        photonView.RPC(
            nameof(RPC_RequestCancelInteraction),
            RpcTarget.MasterClient,
            currentLocalPlayerViewId);
    }

    [PunRPC]
    protected void RPC_RequestBeginInteraction(int playerViewId, PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        if (!ValidateInteractionRequester(playerViewId, info.Sender, out _))
        {
            NotifyRequestRejected(playerViewId, info.Sender);
            return;
        }

        if (isSomeoneInteracting)
        {
            NotifyRequestRejected(playerViewId, info.Sender);
            return;
        }

        BroadcastInteractionStarted(playerViewId, PhotonNetwork.Time);
    }

    [PunRPC]
    protected void RPC_RequestCancelInteraction(int playerViewId, PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        if (!ValidateInteractionRequester(playerViewId, info.Sender, out _))
            return;

        if (!isSomeoneInteracting || interactingPlayerViewId != playerViewId)
            return;

        BroadcastInteractionStopped(playerViewId, PhotonNetwork.Time, HoldInteractionStopReason.Cancelled);
    }

    [PunRPC]
    protected void RPC_RejectInteractionRequest(int playerViewId)
    {
        if (currentLocalPlayerViewId != playerViewId)
            return;

        localRequestPending = false;

        if (!IsLocalPlayerInRange && !IsLocalInteractingPlayer)
            ClearLocalInteractionContext();
    }

    [PunRPC]
    protected void RPC_BeginInteraction(int playerViewId, double startServerTime)
    {
        isSomeoneInteracting = true;
        interactingPlayerViewId = playerViewId;
        interactionStartServerTime = startServerTime;
        lastStateChangeServerTime = startServerTime;
        lastStopReason = HoldInteractionStopReason.None;

        if (currentLocalPlayerViewId == playerViewId)
            localRequestPending = false;
    }

    [PunRPC]
    protected void RPC_StopInteraction(int playerViewId, double stateChangeServerTime, int stopReasonValue)
    {
        isSomeoneInteracting = false;
        interactingPlayerViewId = -1;
        interactionStartServerTime = -1d;
        lastStateChangeServerTime = stateChangeServerTime;
        lastStopReason = (HoldInteractionStopReason)stopReasonValue;

        if (currentLocalPlayerViewId == playerViewId)
            localRequestPending = false;
    }

    private void BroadcastInteractionStarted(int playerViewId, double startServerTime)
    {
        if (PhotonNetwork.InRoom)
        {
            photonView.RPC(
                nameof(RPC_BeginInteraction),
                RpcTarget.All,
                playerViewId,
                startServerTime);
            return;
        }

        RPC_BeginInteraction(playerViewId, startServerTime);
    }

    private void BroadcastInteractionStopped(int playerViewId, double stateChangeServerTime, HoldInteractionStopReason stopReason)
    {
        if (PhotonNetwork.InRoom)
        {
            photonView.RPC(
                nameof(RPC_StopInteraction),
                RpcTarget.All,
                playerViewId,
                stateChangeServerTime,
                (int)stopReason);
            return;
        }

        RPC_StopInteraction(playerViewId, stateChangeServerTime, (int)stopReason);
    }

    private void TickMasterInteraction()
    {
        if (!isSomeoneInteracting)
            return;

        if (PhotonNetwork.InRoom && !PhotonNetwork.IsMasterClient)
            return;

        if (NetworkTime < interactionStartServerTime + holdDuration)
            return;

        BroadcastInteractionStopped(interactingPlayerViewId, NetworkTime, HoldInteractionStopReason.Completed);
    }

    private void HandleReplicatedStateTransitions()
    {
        bool stateChanged =
            previousInteractionActive != isSomeoneInteracting ||
            previousInteractingPlayerViewId != interactingPlayerViewId ||
            previousStateChangeServerTime != lastStateChangeServerTime;

        if (!stateChanged)
            return;

        if (isSomeoneInteracting)
        {
            AcquireLocalMovementLockIfNeeded();
            OnInteractionStartedReplicated(interactingPlayerViewId, interactionStartServerTime);
        }
        else if (previousInteractionActive)
        {
            ReleaseLocalMovementLock();
            OnInteractionStoppedReplicated(
                previousInteractingPlayerViewId,
                lastStopReason,
                lastStateChangeServerTime);

            if (!IsLocalPlayerInRange && !localRequestPending)
                ClearLocalInteractionContext();
        }

        previousInteractionActive = isSomeoneInteracting;
        previousInteractingPlayerViewId = interactingPlayerViewId;
        previousStateChangeServerTime = lastStateChangeServerTime;
    }

    private void UpdateInteractionPresentation()
    {
        float progress = GetCurrentHoldProgress();
        ApplyHoldProgressVisual(progress);
        UpdateVisualState(progress, isSomeoneInteracting, NetworkTime);
        UpdateCanvasVisibility();
    }

    private float GetCurrentHoldProgress()
    {
        if (!isSomeoneInteracting || interactionStartServerTime < 0d || holdDuration <= 0f)
            return 0f;

        return Mathf.Clamp01((float)((NetworkTime - interactionStartServerTime) / holdDuration));
    }

    private void ApplyHoldProgressVisual(float progress)
    {
        if (holdProgressImage != null)
            holdProgressImage.fillAmount = Mathf.Clamp01(progress);
    }

    private void UpdateCanvasVisibility()
    {
        if (interactionCanvas != null)
            interactionCanvas.gameObject.SetActive(isSomeoneInteracting || IsLocalPlayerInRange);
    }

    private void AcquireLocalMovementLockIfNeeded()
    {
        ReleaseLocalMovementLock();

        if (!lockPlayerMovementWhileInteracting || !IsLocalInteractingPlayer || currentLocalPlayerController == null)
            return;

        float expectedDuration = holdDuration
            + Mathf.Max(cancelReturnDuration, GetPostInteractionAnimationDuration())
            + interactionLockBuffer;

        activeMovementLockId = currentLocalPlayerController.ApplyMovementLock(expectedDuration, interactionLockPriority);
    }

    private void ReleaseLocalMovementLock()
    {
        if (activeMovementLockId < 0 || currentLocalPlayerController == null)
            return;

        currentLocalPlayerController.RemoveCommand(activeMovementLockId);
        activeMovementLockId = -1;
    }

    private bool ValidateInteractionRequester(int playerViewId, Player sender, out PlayerController playerController)
    {
        playerController = null;

        PhotonView playerView = PhotonView.Find(playerViewId);
        if (playerView == null)
            return false;

        if (sender != null && playerView.Owner != sender)
            return false;

        PlayerInputSource inputSource = playerView.GetComponent<PlayerInputSource>();
        if (inputSource == null)
            inputSource = playerView.GetComponentInParent<PlayerInputSource>();

        playerController = playerView.GetComponent<PlayerController>();
        if (playerController == null)
            playerController = playerView.GetComponentInParent<PlayerController>();

        return inputSource != null && playerController != null;
    }

    private void NotifyRequestRejected(int playerViewId, Player sender)
    {
        if (sender == null)
            return;

        if (sender.IsLocal)
        {
            RPC_RejectInteractionRequest(playerViewId);
            return;
        }

        photonView.RPC(nameof(RPC_RejectInteractionRequest), sender, playerViewId);
    }

    private int ResolvePhotonViewId(Component component)
    {
        if (component == null)
            return -1;

        PhotonView targetView = component.GetComponent<PhotonView>();
        if (targetView == null)
            targetView = component.GetComponentInParent<PhotonView>();

        return targetView != null ? targetView.ViewID : -1;
    }

    private void ClearLocalInteractionContext()
    {
        currentLocalInputSource = null;
        currentLocalPlayerController = null;
        currentLocalPlayerViewId = -1;
    }

    protected virtual float GetPostInteractionAnimationDuration()
    {
        return 0f;
    }

    protected abstract void ApplyIdleVisualImmediate();
    protected abstract void UpdateVisualState(float holdProgress, bool isInteracting, double networkTime);

    protected virtual void OnInteractionStartedReplicated(int playerViewId, double startServerTime)
    {
    }

    protected virtual void OnInteractionStoppedReplicated(int playerViewId, HoldInteractionStopReason stopReason, double stopServerTime)
    {
    }
}
