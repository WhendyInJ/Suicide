using Photon.Pun;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public class PlayerMovementAnimator : MonoBehaviour
{
    private static readonly int VelocityHash = Animator.StringToHash("Velocity");

    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private Rigidbody targetRigidbody;
    [SerializeField] private PhotonView photonView;
    [SerializeField] private PlayerInputSource inputSource;
    [SerializeField] private PlayerController playerController;

    [Header("Animation")]
    [SerializeField] private string velocityParameterName = "Velocity";
    [SerializeField, Min(0f)] private float inputThreshold = 0.01f;
    [SerializeField, Min(0f)] private float velocityMultiplier = 1f;
    [SerializeField, Min(0f)] private float dampTime = 0.05f;

    private int velocityParameterHash = VelocityHash;

    private void Reset()
    {
        animator = GetComponent<Animator>();
        targetRigidbody = GetComponent<Rigidbody>();
        photonView = GetComponent<PhotonView>();
        inputSource = GetComponent<PlayerInputSource>();
        playerController = GetComponent<PlayerController>();
    }

    private void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        if (targetRigidbody == null)
            targetRigidbody = GetComponent<Rigidbody>();

        if (photonView == null)
            photonView = GetComponent<PhotonView>();

        if (inputSource == null)
            inputSource = GetComponent<PlayerInputSource>();

        if (playerController == null)
            playerController = GetComponent<PlayerController>();

        velocityParameterHash = string.IsNullOrWhiteSpace(velocityParameterName)
            ? VelocityHash
            : Animator.StringToHash(velocityParameterName);
    }

    private void LateUpdate()
    {
        if (animator == null)
            return;

        float targetVelocity = ShouldAnimateFromInput()
            ? GetPlanarSpeed() * velocityMultiplier
            : 0f;

        animator.SetFloat(velocityParameterHash, targetVelocity, dampTime, Time.deltaTime);
    }

    private bool ShouldAnimateFromInput()
    {
        if (!HasLocalAuthority())
            return false;

        if (inputSource == null)
            return false;

        if (playerController != null && playerController.IsInputBlocked)
            return false;

        return inputSource.MoveInput.sqrMagnitude > inputThreshold * inputThreshold;
    }

    private float GetPlanarSpeed()
    {
        if (targetRigidbody == null)
            return 0f;

        Vector3 velocity = targetRigidbody.linearVelocity;
        velocity.y = 0f;
        return velocity.magnitude;
    }

    private bool HasLocalAuthority()
    {
        if (!PhotonNetwork.InRoom)
            return true;

        return photonView != null && photonView.IsMine;
    }
}
