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

    [Header("Inventory Keys")]
    [SerializeField] private KeyCode selectItemSlot1Key = KeyCode.Alpha1;
    [SerializeField] private KeyCode selectItemSlot2Key = KeyCode.Alpha2;

    [Header("Interaction Keys")]
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    [Header("Movement Keys")]
    [SerializeField] private KeyCode jumpKey = KeyCode.Space;

    [Header("Control")]
    [SerializeField] private PlayerController playerController;

    public Vector2 MoveInput { get; private set; }
    public Vector2 LookInput { get; private set; }

    public bool PrimarySkillPressedThisFrame { get; private set; }
    public bool SecondarySkillPressedThisFrame { get; private set; }

    public bool SelectItemSlot1PressedThisFrame { get; private set; }
    public bool SelectItemSlot2PressedThisFrame { get; private set; }
    public bool JumpPressedThisFrame { get; private set; }
    public bool InteractPressedThisFrame { get; private set; }
    public bool InteractReleasedThisFrame { get; private set; }
    public bool InteractHeld { get; private set; }

    public bool ItemPrimaryPressedThisFrame { get; private set; }
    public bool ItemPrimaryReleasedThisFrame { get; private set; }
    public bool ItemPrimaryHeld { get; private set; }

    public bool ItemSecondaryPressedThisFrame { get; private set; }
    public bool ItemSecondaryReleasedThisFrame { get; private set; }
    public bool ItemSecondaryHeld { get; private set; }

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
        playerController = GetComponent<PlayerController>();
    }

    private void Awake()
    {
        if (photonView == null)
            photonView = GetComponent<PhotonView>();

        if (photonView == null)
            photonView = GetComponentInParent<PhotonView>();

        if (playerController == null)
            playerController = GetComponent<PlayerController>();
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

        if (playerController != null && playerController.IsInputBlocked)
        {
            ClearInputs();
            return;
        }

        ReadMoveInput();
        ReadLookInput();
        ReadJumpInput();
        ReadInteractionInput();
        ReadSkillInput();
        ReadInventoryInput();
        ReadItemUseInput();
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

    private void ReadInventoryInput()
    {
        SelectItemSlot1PressedThisFrame = Input.GetKeyDown(selectItemSlot1Key);
        SelectItemSlot2PressedThisFrame = Input.GetKeyDown(selectItemSlot2Key);
    }

    private void ReadJumpInput()
    {
        JumpPressedThisFrame = Input.GetKeyDown(jumpKey);
    }

    private void ReadInteractionInput()
    {
        InteractPressedThisFrame = Input.GetKeyDown(interactKey);
        InteractReleasedThisFrame = Input.GetKeyUp(interactKey);
        InteractHeld = Input.GetKey(interactKey);
    }

    private void ReadItemUseInput()
    {
        ItemPrimaryPressedThisFrame = Input.GetMouseButtonDown(0);
        ItemPrimaryReleasedThisFrame = Input.GetMouseButtonUp(0);
        ItemPrimaryHeld = Input.GetMouseButton(0);

        ItemSecondaryPressedThisFrame = Input.GetMouseButtonDown(1);
        ItemSecondaryReleasedThisFrame = Input.GetMouseButtonUp(1);
        ItemSecondaryHeld = Input.GetMouseButton(1);
    }

    private void ClearInputs()
    {
        MoveInput = Vector2.zero;
        LookInput = Vector2.zero;

        PrimarySkillPressedThisFrame = false;
        SecondarySkillPressedThisFrame = false;

        SelectItemSlot1PressedThisFrame = false;
        SelectItemSlot2PressedThisFrame = false;
        JumpPressedThisFrame = false;
        InteractPressedThisFrame = false;
        InteractReleasedThisFrame = false;
        InteractHeld = false;

        ItemPrimaryPressedThisFrame = false;
        ItemPrimaryReleasedThisFrame = false;
        ItemPrimaryHeld = false;

        ItemSecondaryPressedThisFrame = false;
        ItemSecondaryReleasedThisFrame = false;
        ItemSecondaryHeld = false;
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

    public bool GetInventorySlotPressedThisFrame(int slotIndex)
    {
        switch (slotIndex)
        {
            case 0:
                return SelectItemSlot1PressedThisFrame;
            case 1:
                return SelectItemSlot2PressedThisFrame;
            default:
                return false;
        }
    }
}
