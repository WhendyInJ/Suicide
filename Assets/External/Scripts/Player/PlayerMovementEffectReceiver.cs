using Photon.Pun;
using UnityEngine;

[RequireComponent(typeof(PhotonView))]
public class PlayerMovementEffectReceiver : MonoBehaviourPun
{
    [SerializeField] private PlayerController playerController;

    private void Reset()
    {
        playerController = GetComponent<PlayerController>();
    }

    private void Awake()
    {
        if (playerController == null)
            playerController = GetComponent<PlayerController>();
    }

    public void RequestImpulseKnockback(
        Vector3 worldImpulse,
        ImpulseControlReleaseMode releaseMode,
        float releaseTimeout = 0f,
        bool faceDirection = true,
        bool clearExistingCommands = true)
    {
        if (PhotonNetwork.InRoom && photonView.Owner != null)
        {
            photonView.RPC(
                nameof(RPC_ApplyImpulseKnockback),
                photonView.Owner,
                worldImpulse,
                (int)releaseMode,
                releaseTimeout,
                faceDirection,
                clearExistingCommands);
        }
        else
        {
            RPC_ApplyImpulseKnockback(
                worldImpulse,
                (int)releaseMode,
                releaseTimeout,
                faceDirection,
                clearExistingCommands);
        }
    }

    public void RequestEffect(
        MovementEffectType effectType,
        Vector3 vectorValue,
        float speed,
        float duration,
        float stopDistance = 0.1f,
        int priority = 100,
        bool faceDirection = true)
    {
        if (PhotonNetwork.InRoom && photonView.Owner != null)
        {
            photonView.RPC(
                nameof(RPC_ApplyEffect),
                photonView.Owner,
                (int)effectType,
                vectorValue,
                speed,
                duration,
                stopDistance,
                priority,
                faceDirection);
        }
        else
        {
            RPC_ApplyEffect(
                (int)effectType,
                vectorValue,
                speed,
                duration,
                stopDistance,
                priority,
                faceDirection);
        }
    }

    public void RequestMovementLock(
        float duration,
        int priority = 300)
    {
        RequestEffect(
            MovementEffectType.MovementLock,
            Vector3.zero,
            0f,
            duration,
            0f,
            priority,
            false);
    }

    public void RequestPullToPoint(
        Vector3 worldPoint,
        float speed,
        float duration,
        float stopDistance = 0.1f,
        int priority = 150,
        bool faceDirection = true)
    {
        RequestEffect(
            MovementEffectType.PullToPoint,
            worldPoint,
            speed,
            duration,
            stopDistance,
            priority,
            faceDirection);
    }

    public void RequestStun(
        float duration,
        int priority = 300)
    {
        if (PhotonNetwork.InRoom && photonView.Owner != null)
        {
            photonView.RPC(
                nameof(RPC_ApplyStun),
                photonView.Owner,
                duration,
                priority);
        }
        else
        {
            RPC_ApplyStun(duration, priority);
        }
    }

    [PunRPC]
    private void RPC_ApplyImpulseKnockback(
        Vector3 worldImpulse,
        int releaseModeRaw,
        float releaseTimeout,
        bool faceDirection,
        bool clearExistingCommands)
    {
        if (PhotonNetwork.InRoom && !photonView.IsMine)
            return;

        if (playerController == null)
            return;

        playerController.ApplyImpulseKnockback(
            worldImpulse,
            (ImpulseControlReleaseMode)releaseModeRaw,
            releaseTimeout,
            faceDirection,
            clearExistingCommands);
    }

    [PunRPC]
    private void RPC_ApplyEffect(
        int effectTypeRaw,
        Vector3 vectorValue,
        float speed,
        float duration,
        float stopDistance,
        int priority,
        bool faceDirection)
    {
        if (PhotonNetwork.InRoom && !photonView.IsMine)
            return;

        if (playerController == null)
            return;

        playerController.ApplyEffect(
            (MovementEffectType)effectTypeRaw,
            vectorValue,
            speed,
            duration,
            stopDistance,
            priority,
            faceDirection);
    }

    [PunRPC]
    private void RPC_ApplyStun(
        float duration,
        int priority)
    {
        if (PhotonNetwork.InRoom && !photonView.IsMine)
            return;

        if (playerController == null)
            return;

        playerController.ApplyStun(duration, priority);
    }
}