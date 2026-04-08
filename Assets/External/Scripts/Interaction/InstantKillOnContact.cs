using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class InstantKillOnContact : MonoBehaviour
{
    [Header("Options")]
    [SerializeField] private string requiredTag = "Player";

    private void OnTriggerEnter(Collider other)
    {
        TryKill(other);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision == null)
            return;

        TryKill(collision.collider);
    }

    private void TryKill(Collider other)
    {
        if (other == null)
            return;

        if (!string.IsNullOrEmpty(requiredTag) && !other.CompareTag(requiredTag))
            return;

        PlayerHealth health = other.GetComponentInParent<PlayerHealth>();
        if (health == null)
            return;

        health.RequestKill();
    }
}
