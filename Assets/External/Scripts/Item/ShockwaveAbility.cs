using UnityEngine;

public class ShockwaveAbility : MonoBehaviour
{
    public float radius = 6f;
    public float force = 25f;

    public void Execute()
    {
        Collider[] cols = Physics.OverlapSphere(transform.position, radius);

        foreach (var col in cols)
        {
            Rigidbody rb = col.attachedRigidbody;
            if (rb == null) continue;

            Vector3 dir = (rb.position - transform.position).normalized;
            dir.y = 0;

            rb.AddForce(dir * force, ForceMode.Impulse);
        }
    }
}