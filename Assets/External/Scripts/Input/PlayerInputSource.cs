using Photon.Pun;
using UnityEngine;

[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(PhotonView))]
public class PlayerInputSource : MonoBehaviour
{
    [Header("Cursor")]
    [SerializeField] private bool lockCursorOnStart = true;

    [Header("Look")]
    [SerializeField] private float lookSensitivity = 1f;

    [Header("Basic Skill Keys")]
    [SerializeField] private KeyCode primarySkillKey = KeyCode.F;
    [SerializeField] private KeyCode secondarySkillKey = KeyCode.G;

    public Vector2 MoveInput { get; private set; }
    public Vector2 LookInput { get; private set; }

    public bool PrimarySkillPressedThisFrame { get; private set; }
    public bool SecondarySkillPressedThisFrame { get; private set; }

    private PhotonView photonView;

    public bool HasLocalAuthority
    {
        get
        {
            if (!PhotonNetwork.InRoom)
                return true;

            return photonView != null && photonView.IsMine;
        }
    }

    private void Reset()
    {
        photonView = GetComponent<PhotonView>();
    }

    private void Awake()
    {
        if (photonView == null)
            photonView = GetComponent<PhotonView>();

        if (photonView == null)
            photonView = GetComponentInParent<PhotonView>();
    }

    private void Start()
    {
        if (lockCursorOnStart && HasLocalAuthority)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void Update()
    {
        if (!HasLocalAuthority)
        {
            ClearInputs();
            return;
        }

        ReadMoveInput();
        ReadLookInput();
        ReadSkillInput();
    }

    private void ReadMoveInput()
    {
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveY = Input.GetAxisRaw("Vertical");

        MoveInput = Vector2.ClampMagnitude(new Vector2(moveX, moveY), 1f);
    }

    private void ReadLookInput()
    {
        float lookX = Input.GetAxis("Mouse X");
        float lookY = Input.GetAxis("Mouse Y");

        LookInput = new Vector2(lookX, lookY) * lookSensitivity;
    }

    private void ReadSkillInput()
    {
        PrimarySkillPressedThisFrame = Input.GetKeyDown(primarySkillKey);
        SecondarySkillPressedThisFrame = Input.GetKeyDown(secondarySkillKey);
    }

    private void ClearInputs()
    {
        MoveInput = Vector2.zero;
        LookInput = Vector2.zero;
        PrimarySkillPressedThisFrame = false;
        SecondarySkillPressedThisFrame = false;
    }

    public bool GetSkillPressedThisFrame(BasicSkillSlotType slot)
    {
        switch (slot)
        {
            case BasicSkillSlotType.Primary:
                return PrimarySkillPressedThisFrame;

            case BasicSkillSlotType.Secondary:
                return SecondarySkillPressedThisFrame;

            default:
                return false;
        }
    }
}