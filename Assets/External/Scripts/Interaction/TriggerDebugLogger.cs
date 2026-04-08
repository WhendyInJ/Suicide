using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class TriggerDebugLogger : MonoBehaviour
{
    [SerializeField] private bool logEnter = true;
    [SerializeField] private bool logStay = false;
    [SerializeField] private bool logExit = true;

    private void Reset()
    {
        Collider trigger = GetComponent<Collider>();
        if (trigger != null)
            trigger.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!logEnter)
            return;

        Debug.Log($"[TriggerDebugLogger:{name}] Enter -> {other.name}", this);
    }

    private void OnTriggerStay(Collider other)
    {
        if (!logStay)
            return;

        Debug.Log($"[TriggerDebugLogger:{name}] Stay -> {other.name}", this);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!logExit)
            return;

        Debug.Log($"[TriggerDebugLogger:{name}] Exit -> {other.name}", this);
    }
}
