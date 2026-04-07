using UnityEngine;
using Photon.Pun;

[DefaultExecutionOrder(-100)]
public class PlayerInputSource : MonoBehaviour
{
    [Header("Cursor")]
    [SerializeField] private bool lockCursorOnStart = true;

    [Header("Legacy Input Sample")]
    [SerializeField] private float lookSensitivity = 1f;

    public Vector2 MoveInput { get; private set; }
    public Vector2 LookInput { get; private set; }

    private PhotonView photonView;

    private void Awake()
    {
        photonView = GetComponent<PhotonView>();

        // PlayerInputSource가 자식 오브젝트에 붙어있고 PhotonView가 부모에 있다면 이걸 쓰면 됨.
        if (photonView == null)
            photonView = GetComponentInParent<PhotonView>();

        // 네트워크 플레이 중이고 내 오브젝트가 아니면 입력 컴포넌트를 비활성화
        if (PhotonNetwork.IsConnected && photonView != null && !photonView.IsMine)
        {
            MoveInput = Vector2.zero;
            LookInput = Vector2.zero;
            enabled = false;
        }
    }

    private void Start()
    {
        // 내 플레이어일 때만 커서 잠금
        if (lockCursorOnStart && IsLocalInputOwner())
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void Update()
    {
        if (!IsLocalInputOwner())
        {
            MoveInput = Vector2.zero;
            LookInput = Vector2.zero;
            return;
        }

        float moveX = Input.GetAxisRaw("Horizontal");
        float moveY = Input.GetAxisRaw("Vertical");
        MoveInput = Vector2.ClampMagnitude(new Vector2(moveX, moveY), 1f);

        float lookX = Input.GetAxis("Mouse X");
        float lookY = Input.GetAxis("Mouse Y");
        LookInput = new Vector2(lookX, lookY) * lookSensitivity;
    }

    private bool IsLocalInputOwner()
    {
        // PUN 없이 단독 테스트할 때는 그냥 입력 허용
        if (!PhotonNetwork.IsConnected)
            return true;

        // PhotonView가 없으면 안전하게 입력 차단
        if (photonView == null)
            return false;

        return photonView.IsMine;
    }
}