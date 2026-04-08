using System.Collections;
using Photon.Pun;
using UnityEngine;

public class PlayerItemRunner : PhotonOwnedBehaviour, IItemExecutionBridge
{
    private sealed class RuntimeSlot
    {
        public ItemDefinition BoundDefinition;
        public IItemRuntime Runtime;
    }

    private const int NoHeldVisualViewId = -1;

    [Header("References")]
    [SerializeField] private PlayerItemInventory inventory;
    [SerializeField] private PlayerInputSource inputSource;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private PlayerMovementEffectReceiver selfMovementReceiver;
    [SerializeField] private Transform castOrigin;
    [SerializeField] private Transform effectSpawnPoint;
    [SerializeField] private Camera aimCamera;

    [Header("Held Visual")]
    [SerializeField] private Transform heldItemAnchor;

    [Header("Aim")]
    [SerializeField, Min(0.1f)] private float defaultAimDistance = 30f;

    public bool IsAimingItem => isAimingItem;

    public event System.Action<bool> OnAimStateChanged;

    private RuntimeSlot[] runtimeSlots;
    private bool isAimingItem;
    private int aimingSlotIndex = -1;

    private ItemDefinition currentHeldDefinition;
    private GameObject currentHeldVisualInstance;
    private int currentHeldVisualViewId = NoHeldVisualViewId;
    private Coroutine waitForHeldVisualCoroutine;

    protected override void Awake()
    {
        base.Awake();

        if (inventory == null)
            inventory = GetComponent<PlayerItemInventory>();

        if (inputSource == null)
            inputSource = GetComponent<PlayerInputSource>();

        if (playerController == null)
            playerController = GetComponent<PlayerController>();

        if (selfMovementReceiver == null)
            selfMovementReceiver = GetComponent<PlayerMovementEffectReceiver>();

        if (castOrigin == null)
            castOrigin = transform;

        if (effectSpawnPoint == null)
            effectSpawnPoint = castOrigin;

        if (aimCamera == null)
            aimCamera = Camera.main;

        runtimeSlots = new RuntimeSlot[2];
        for (int i = 0; i < runtimeSlots.Length; i++)
        {
            runtimeSlots[i] = new RuntimeSlot();
        }

        RebuildAllRuntimes();
        RefreshHeldItemVisual();
    }

    private void OnEnable()
    {
        if (inventory != null)
        {
            inventory.OnInventoryChanged += HandleInventoryChanged;
            inventory.OnSelectedSlotChanged += HandleSelectedSlotChanged;
        }
    }

    private void OnDisable()
    {
        if (inventory != null)
        {
            inventory.OnInventoryChanged -= HandleInventoryChanged;
            inventory.OnSelectedSlotChanged -= HandleSelectedSlotChanged;
        }
    }

    private void Update()
    {
        if (!HasLocalAuthority)
            return;

        TickRuntimes(Time.deltaTime);
        HandleSlotSelectionInput();
        HandleItemUseInput();
    }

    public bool TryUseSelectedItemPrimary()
    {
        if (inventory == null)
            return false;

        return TryUseSlotPrimary(inventory.SelectedSlotIndex);
    }

    private void HandleInventoryChanged()
    {
        RebuildAllRuntimes();
        RefreshHeldItemVisual();

        if (inventory == null)
            return;

        if (!inventory.TryGetSelectedItem(out _, out _))
        {
            CancelAim();
        }
    }

    private void HandleSelectedSlotChanged(int selectedIndex)
    {
        RefreshHeldItemVisual();

        if (isAimingItem && aimingSlotIndex != selectedIndex)
        {
            CancelAim();
        }
    }

    private void TickRuntimes(float deltaTime)
    {
        EnsureAllRuntimesUpToDate();

        for (int i = 0; i < runtimeSlots.Length; i++)
        {
            if (runtimeSlots[i].Runtime != null)
            {
                runtimeSlots[i].Runtime.Tick(deltaTime);
            }
        }
    }

    private void HandleSlotSelectionInput()
    {
        if (inventory == null || inputSource == null)
            return;

        if (inputSource.GetInventorySlotPressedThisFrame(0))
        {
            HandleSlotSelectionRequest(0);
        }

        if (inputSource.GetInventorySlotPressedThisFrame(1))
        {
            HandleSlotSelectionRequest(1);
        }
    }

    private void HandleSlotSelectionRequest(int slotIndex)
    {
        if (inventory == null)
            return;

        if (inventory.SelectedSlotIndex == slotIndex)
        {
            inventory.ClearSelection();
            return;
        }

        if (inventory.TrySelectSlot(slotIndex))
            return;

        inventory.ClearSelection();
    }

    private void HandleItemUseInput()
    {
        if (inventory == null || inputSource == null)
            return;

        if (!inventory.HasSelection)
        {
            CancelAim();
            return;
        }

        int selectedSlot = inventory.SelectedSlotIndex;
        if (!inventory.TryGetSlotInfo(selectedSlot, out ItemDefinition definition, out _))
        {
            CancelAim();
            return;
        }

        EnsureRuntimeForSlot(selectedSlot);

        if (!IsValidRuntimeSlotIndex(selectedSlot))
            return;

        RuntimeSlot runtimeSlot = runtimeSlots[selectedSlot];
        if (runtimeSlot == null || runtimeSlot.Runtime == null)
            return;

        if (runtimeSlot.Runtime.UseMode == ItemUseMode.Instant)
        {
            if (inputSource.ItemPrimaryPressedThisFrame)
            {
                TryUseSlotPrimary(selectedSlot);
            }

            return;
        }

        if (runtimeSlot.Runtime.UseMode == ItemUseMode.Aimed)
        {
            if (inputSource.ItemSecondaryPressedThisFrame)
            {
                BeginAim(selectedSlot);
            }

            if (!isAimingItem || aimingSlotIndex != selectedSlot)
                return;

            if (inputSource.ItemSecondaryReleasedThisFrame)
            {
                CancelAim();
                return;
            }

            if (inputSource.ItemPrimaryPressedThisFrame)
            {
                TryUseSlotPrimary(selectedSlot);
            }
        }
    }

    private bool TryUseSlotPrimary(int slotIndex)
    {
        if (!TryGetUsableRuntime(slotIndex, out RuntimeSlot runtimeSlot))
            return false;

        IItemRuntime runtime = runtimeSlot.Runtime;
        if (runtime == null)
            return false;

        ItemUseMode useMode = runtime.UseMode;
        if (useMode == ItemUseMode.Aimed)
        {
            if (!isAimingItem || aimingSlotIndex != slotIndex)
                return false;
        }

        ItemUseRequest request = BuildUseRequest(runtime);
        bool used = runtime.TryUse(request);
        if (!used)
            return false;

        if (inventory == null)
            return false;

        bool consumed = inventory.TryConsumeSlot(slotIndex, 1);
        if (!consumed)
            return false;

        if (useMode == ItemUseMode.Aimed)
        {
            CancelAim();
        }

        if (inventory != null)
        {
            inventory.ClearSelection();
        }

        EnsureRuntimeForSlot(slotIndex);
        RefreshHeldItemVisual();
        return true;
    }

    private bool TryGetUsableRuntime(int slotIndex, out RuntimeSlot runtimeSlot)
    {
        runtimeSlot = null;

        if (inventory == null || !inventory.TryGetSlotInfo(slotIndex, out _, out _))
            return false;

        EnsureRuntimeForSlot(slotIndex);

        if (!IsValidRuntimeSlotIndex(slotIndex))
            return false;

        runtimeSlot = runtimeSlots[slotIndex];
        if (runtimeSlot == null || runtimeSlot.Runtime == null)
            return false;

        return runtimeSlot.Runtime.CanUse();
    }

    private bool IsValidRuntimeSlotIndex(int slotIndex)
    {
        return runtimeSlots != null && slotIndex >= 0 && slotIndex < runtimeSlots.Length;
    }

    private void BeginAim(int slotIndex)
    {
        if (aimingSlotIndex == slotIndex && isAimingItem)
            return;

        isAimingItem = true;
        aimingSlotIndex = slotIndex;
        OnAimStateChanged?.Invoke(true);
    }

    private void CancelAim()
    {
        if (!isAimingItem)
            return;

        isAimingItem = false;
        aimingSlotIndex = -1;
        OnAimStateChanged?.Invoke(false);
    }

    private ItemUseRequest BuildUseRequest(IItemRuntime runtime)
    {
        Transform originTransform = castOrigin != null ? castOrigin : transform;

        Vector3 useOrigin = originTransform.position;
        Vector3 aimDirection = originTransform.forward;
        Vector3 aimPoint = useOrigin + aimDirection * defaultAimDistance;
        GameObject explicitTarget = null;

        Ray aimRay = BuildAimRay(originTransform);

        switch (runtime.AimSettings.AimType)
        {
            case ItemAimType.WorldPoint:
                aimPoint = ResolveAimPoint(aimRay, runtime.AimSettings);
                aimDirection = (aimPoint - useOrigin).sqrMagnitude > 0.0001f
                    ? (aimPoint - useOrigin).normalized
                    : originTransform.forward;
                break;

            case ItemAimType.WorldDirection:
                aimDirection = aimRay.direction.normalized;
                aimPoint = useOrigin + aimDirection * Mathf.Max(defaultAimDistance, runtime.AimSettings.MaxDistance);
                break;

            case ItemAimType.Target:
                if (ResolveAimTarget(aimRay, runtime.AimSettings, out RaycastHit targetHit))
                {
                    explicitTarget = targetHit.collider != null ? targetHit.collider.gameObject : null;
                    aimPoint = targetHit.point;
                    aimDirection = (aimPoint - useOrigin).sqrMagnitude > 0.0001f
                        ? (aimPoint - useOrigin).normalized
                        : originTransform.forward;
                }
                break;

            case ItemAimType.None:
            default:
                aimDirection = originTransform.forward;
                aimPoint = useOrigin + aimDirection * defaultAimDistance;
                break;
        }

        return new ItemUseRequest(useOrigin, aimPoint, aimDirection, explicitTarget);
    }

    private Ray BuildAimRay(Transform originTransform)
    {
        if (aimCamera != null)
        {
            return new Ray(aimCamera.transform.position, aimCamera.transform.forward);
        }

        return new Ray(originTransform.position, originTransform.forward);
    }

    private Vector3 ResolveAimPoint(Ray ray, ItemAimSettings settings)
    {
        float maxDistance = settings.MaxDistance > 0f ? settings.MaxDistance : defaultAimDistance;

        if (Raycast(ray, maxDistance, settings.AimMask, settings.TriggerInteraction, out RaycastHit hit))
        {
            return hit.point;
        }

        return ray.GetPoint(maxDistance);
    }

    private bool ResolveAimTarget(Ray ray, ItemAimSettings settings, out RaycastHit hit)
    {
        float maxDistance = settings.MaxDistance > 0f ? settings.MaxDistance : defaultAimDistance;
        return Raycast(ray, maxDistance, settings.AimMask, settings.TriggerInteraction, out hit);
    }

    private void RebuildAllRuntimes()
    {
        if (runtimeSlots == null || runtimeSlots.Length != 2)
        {
            runtimeSlots = new RuntimeSlot[2];
            for (int i = 0; i < runtimeSlots.Length; i++)
            {
                runtimeSlots[i] = new RuntimeSlot();
            }
        }

        for (int i = 0; i < runtimeSlots.Length; i++)
        {
            EnsureRuntimeForSlot(i);
        }
    }

    private void EnsureAllRuntimesUpToDate()
    {
        for (int i = 0; i < runtimeSlots.Length; i++)
        {
            EnsureRuntimeForSlot(i);
        }
    }

    private void EnsureRuntimeForSlot(int slotIndex)
    {
        if (runtimeSlots == null || slotIndex < 0 || slotIndex >= runtimeSlots.Length)
            return;

        RuntimeSlot slotRuntime = runtimeSlots[slotIndex];

        if (inventory == null || !inventory.TryGetSlotInfo(slotIndex, out ItemDefinition currentDefinition, out _))
        {
            slotRuntime.BoundDefinition = null;
            slotRuntime.Runtime = null;
            return;
        }

        if (slotRuntime.BoundDefinition == currentDefinition && slotRuntime.Runtime != null)
            return;

        ItemRuntimeContext context = new ItemRuntimeContext(
            transform,
            castOrigin != null ? castOrigin : transform,
            effectSpawnPoint != null ? effectSpawnPoint : castOrigin,
            playerController,
            selfMovementReceiver,
            this);

        slotRuntime.BoundDefinition = currentDefinition;
        slotRuntime.Runtime = currentDefinition != null
            ? currentDefinition.CreateRuntime(context)
            : null;
    }

    private void RefreshHeldItemVisual()
    {
        if (PhotonNetwork.InRoom && !HasLocalAuthority)
            return;

        if (heldItemAnchor == null)
        {
            ClearHeldItemVisual();
            return;
        }

        if (inventory == null || !inventory.TryGetSelectedItem(out ItemDefinition definition, out _))
        {
            ClearHeldItemVisual();
            return;
        }

        HeldItemVisualData heldVisual = definition.HeldVisual;
        if (!heldVisual.HasPrefab)
        {
            ClearHeldItemVisual();
            return;
        }

        if (currentHeldDefinition == definition && currentHeldVisualInstance != null)
            return;

        if (PhotonNetwork.InRoom)
        {
            RefreshNetworkHeldItemVisual(definition, heldVisual);
            return;
        }

        ClearHeldItemVisual();

        currentHeldVisualInstance = Instantiate(heldVisual.Prefab, heldItemAnchor);
        ApplyHeldVisualTransform(
            currentHeldVisualInstance,
            heldVisual.LocalPosition,
            heldVisual.LocalEulerAngles,
            heldVisual.LocalScale);
        currentHeldDefinition = definition;
    }

    private void RefreshNetworkHeldItemVisual(ItemDefinition definition, HeldItemVisualData heldVisual)
    {
        if (definition == null || !heldVisual.HasPrefab)
        {
            ClearHeldItemVisual();
            return;
        }

        if (CachedPhotonView == null)
        {
            Debug.LogWarning("Cannot network-equip held item because this player has no PhotonView.", this);
            return;
        }

        ClearHeldItemVisual();

        GameObject spawned = PhotonNetwork.Instantiate(
            heldVisual.Prefab.name,
            heldItemAnchor.position,
            heldItemAnchor.rotation);

        if (spawned == null)
            return;

        PhotonView heldView = spawned.GetComponent<PhotonView>();
        if (heldView == null)
        {
            Debug.LogWarning($"Held item prefab '{heldVisual.Prefab.name}' needs a PhotonView for network equip.", this);
            Destroy(spawned);
            return;
        }

        currentHeldVisualInstance = spawned;
        currentHeldDefinition = definition;
        currentHeldVisualViewId = heldView.ViewID;

        ApplyHeldVisualTransform(
            spawned,
            heldVisual.LocalPosition,
            heldVisual.LocalEulerAngles,
            heldVisual.LocalScale);

        CachedPhotonView.RPC(
            nameof(RPC_AttachHeldItemVisual),
            RpcTarget.OthersBuffered,
            heldView.ViewID,
            definition.ItemId,
            heldVisual.LocalPosition,
            heldVisual.LocalEulerAngles,
            heldVisual.LocalScale);
    }

    private void ClearHeldItemVisual()
    {
        StopWaitingForHeldVisual();

        if (currentHeldVisualInstance != null)
        {
            if (PhotonNetwork.InRoom)
            {
                PhotonView heldView = currentHeldVisualInstance.GetComponent<PhotonView>();
                int viewId = heldView != null ? heldView.ViewID : currentHeldVisualViewId;

                if (HasLocalAuthority && heldView != null && heldView.IsMine)
                {
                    if (CachedPhotonView != null)
                    {
                        CachedPhotonView.RPC(
                            nameof(RPC_ClearHeldItemVisual),
                            RpcTarget.OthersBuffered,
                            viewId);
                    }

                    PhotonNetwork.Destroy(currentHeldVisualInstance);
                }
                else
                {
                    currentHeldVisualInstance.SetActive(false);
                }
            }
            else
            {
                Destroy(currentHeldVisualInstance);
            }
        }

        ClearHeldItemVisualReference(false);
    }

    [PunRPC]
    private void RPC_AttachHeldItemVisual(
        int heldViewId,
        string itemId,
        Vector3 localPosition,
        Vector3 localEulerAngles,
        Vector3 localScale)
    {
        if (HasLocalAuthority)
            return;

        StopWaitingForHeldVisual();
        ClearHeldItemVisualReference(true);

        currentHeldVisualViewId = heldViewId;
        currentHeldDefinition = ItemDefinitionLookup.GetById(itemId);

        if (TryAttachNetworkHeldVisual(heldViewId, itemId, localPosition, localEulerAngles, localScale))
            return;

        waitForHeldVisualCoroutine = StartCoroutine(
            WaitForHeldVisualAndAttach(heldViewId, itemId, localPosition, localEulerAngles, localScale));
    }

    [PunRPC]
    private void RPC_ClearHeldItemVisual(int heldViewId)
    {
        if (HasLocalAuthority)
            return;

        if (currentHeldVisualViewId != NoHeldVisualViewId && currentHeldVisualViewId != heldViewId)
            return;

        StopWaitingForHeldVisual();

        if (currentHeldVisualInstance != null)
        {
            currentHeldVisualInstance.SetActive(false);
        }

        ClearHeldItemVisualReference(false);
    }

    private IEnumerator WaitForHeldVisualAndAttach(
        int heldViewId,
        string itemId,
        Vector3 localPosition,
        Vector3 localEulerAngles,
        Vector3 localScale)
    {
        float timeoutAt = Time.time + 2f;

        while (Time.time < timeoutAt)
        {
            if (currentHeldVisualViewId != heldViewId)
                break;

            if (TryAttachNetworkHeldVisual(heldViewId, itemId, localPosition, localEulerAngles, localScale))
                break;

            yield return null;
        }

        waitForHeldVisualCoroutine = null;
    }

    private bool TryAttachNetworkHeldVisual(
        int heldViewId,
        string itemId,
        Vector3 localPosition,
        Vector3 localEulerAngles,
        Vector3 localScale)
    {
        PhotonView heldView = PhotonView.Find(heldViewId);
        if (heldView == null)
            return false;

        currentHeldVisualInstance = heldView.gameObject;
        currentHeldVisualViewId = heldViewId;
        currentHeldDefinition = ItemDefinitionLookup.GetById(itemId);

        ApplyHeldVisualTransform(
            currentHeldVisualInstance,
            localPosition,
            localEulerAngles,
            localScale);

        currentHeldVisualInstance.SetActive(true);
        return true;
    }

    private void ApplyHeldVisualTransform(
        GameObject heldInstance,
        Vector3 localPosition,
        Vector3 localEulerAngles,
        Vector3 localScale)
    {
        if (heldInstance == null || heldItemAnchor == null)
            return;

        heldInstance.transform.SetParent(heldItemAnchor, false);
        heldInstance.transform.localPosition = localPosition;
        heldInstance.transform.localRotation = Quaternion.Euler(localEulerAngles);
        heldInstance.transform.localScale = localScale == Vector3.zero ? Vector3.one : localScale;
    }

    private void StopWaitingForHeldVisual()
    {
        if (waitForHeldVisualCoroutine == null)
            return;

        StopCoroutine(waitForHeldVisualCoroutine);
        waitForHeldVisualCoroutine = null;
    }

    private void ClearHeldItemVisualReference(bool deactivateInstance)
    {
        if (deactivateInstance && currentHeldVisualInstance != null)
        {
            currentHeldVisualInstance.SetActive(false);
        }

        currentHeldVisualInstance = null;
        currentHeldDefinition = null;
        currentHeldVisualViewId = NoHeldVisualViewId;
    }

    public bool Raycast(
        Ray ray,
        float maxDistance,
        LayerMask layerMask,
        QueryTriggerInteraction triggerInteraction,
        out RaycastHit hit)
    {
        return Physics.Raycast(ray, out hit, maxDistance, layerMask, triggerInteraction);
    }

    public void SpawnNetworkEffect(GameObject effectPrefab, Vector3 position, Quaternion rotation)
    {
        if (effectPrefab == null)
            return;

        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.Instantiate(effectPrefab.name, position, rotation);
        }
        else
        {
            Instantiate(effectPrefab, position, rotation);
        }
    }

    public GameObject SpawnNetworkObject(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null)
            return null;

        if (PhotonNetwork.InRoom)
        {
            return PhotonNetwork.Instantiate(prefab.name, position, rotation);
        }

        return Instantiate(prefab, position, rotation);
    }
}
