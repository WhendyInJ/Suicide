using UnityEngine;

public class PlayerMove : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;

    private void Update()
    {
        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");

        Vector3 moveDir = new Vector3(x, 0f, z).normalized;
        transform.position += moveDir * moveSpeed * Time.deltaTime;
    }
}
