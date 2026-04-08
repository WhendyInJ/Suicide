using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

/// <summary>
/// Kills players overlapping this collider after a short grace window.
/// If several players are eligible at once, optional lever holders survive; others are killed.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class InstantKillOnContact : MonoBehaviour
{
    [Header("Timing")]
    [SerializeField, Min(0f)] private float graceSeconds = 0.12f;

    [Header("Simultaneous tie-break")]
    [Tooltip("If set, only these levers count. If empty and auto-discover is on, all LeverHoldInteraction in loaded scenes are used.")]
    [SerializeField] private LeverHoldInteraction[] leverHoldInteractionsForTieBreak;

    [SerializeField]
    [Tooltip("When the list above is empty, find every LeverHoldInteraction in the scene at startup.")]
    private bool autoDiscoverLeversWhenEmpty = true;

    private NetworkHoldInteractionBase[] effectiveLevers = System.Array.Empty<NetworkHoldInteractionBase>();

    private readonly HashSet<PlayerHealth> overlapping = new();
    private readonly Dictionary<PlayerHealth, double> contactStartTime = new();
    private readonly List<PlayerHealth> workList = new();
    private readonly List<PlayerHealth> pruneBuffer = new();

    private static double NetworkTime =>
        PhotonNetwork.InRoom ? PhotonNetwork.Time : Time.timeAsDouble;

    private void Awake()
    {
        if (leverHoldInteractionsForTieBreak != null && leverHoldInteractionsForTieBreak.Length > 0)
        {
            effectiveLevers = leverHoldInteractionsForTieBreak;
            return;
        }

        if (autoDiscoverLeversWhenEmpty)
        {
            LeverHoldInteraction[] found = FindObjectsByType<LeverHoldInteraction>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            effectiveLevers = found != null && found.Length > 0 ? found : System.Array.Empty<NetworkHoldInteractionBase>();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        RegisterContact(other);
    }

    private void OnTriggerExit(Collider other)
    {
        UnregisterContact(other);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision != null)
            RegisterContact(collision.collider);
    }

    private void OnCollisionExit(Collision collision)
    {
        if (collision != null)
            UnregisterContact(collision.collider);
    }

    private void LateUpdate()
    {
        PruneDestroyed();
        if (overlapping.Count == 0)
            return;

        double now = NetworkTime;
        workList.Clear();

        foreach (PlayerHealth health in overlapping)
        {
            if (health == null || health.IsDead)
                continue;

            if (!contactStartTime.TryGetValue(health, out double t0))
                continue;

            if (now < t0 + graceSeconds)
                continue;

            workList.Add(health);
        }

        if (workList.Count == 0)
            return;

        ResolveBatch(now);
    }

    private void ResolveBatch(double now)
    {
        if (workList.Count == 1)
        {
            workList[0].RequestKill();
            return;
        }

        workList.Sort(CompareByActorNumber);

        PlayerHealth winner = null;
        for (int i = 0; i < workList.Count; i++)
        {
            PlayerHealth candidate = workList[i];
            int viewId = GetPlayerViewId(candidate);
            if (viewId > 0 && IsViewIdHoldingConfiguredLever(viewId))
            {
                winner = candidate;
                break;
            }
        }

        if (winner != null)
        {
            for (int i = 0; i < workList.Count; i++)
            {
                PlayerHealth health = workList[i];
                if (health != winner)
                    health.RequestKill();
            }

            if (contactStartTime.ContainsKey(winner))
                contactStartTime[winner] = now;

            return;
        }

        for (int i = 0; i < workList.Count; i++)
            workList[i].RequestKill();
    }

    private static int CompareByActorNumber(PlayerHealth a, PlayerHealth b)
    {
        int na = a != null ? a.OwnerActorNumber : int.MaxValue;
        int nb = b != null ? b.OwnerActorNumber : int.MaxValue;
        return na.CompareTo(nb);
    }

    private bool IsViewIdHoldingConfiguredLever(int playerViewId)
    {
        if (effectiveLevers == null || effectiveLevers.Length == 0)
            return false;

        for (int i = 0; i < effectiveLevers.Length; i++)
        {
            NetworkHoldInteractionBase lever = effectiveLevers[i];
            if (lever == null)
                continue;

            if (lever.IsBeingHeld && lever.HolderPlayerViewId == playerViewId)
                return true;
        }

        return false;
    }

    private static int GetPlayerViewId(PlayerHealth health)
    {
        if (health == null)
            return -1;

        PhotonView pv = health.GetComponent<PhotonView>();
        if (pv == null)
            pv = health.GetComponentInParent<PhotonView>();

        return pv != null ? pv.ViewID : -1;
    }

    private void RegisterContact(Collider other)
    {
        if (other == null)
            return;

        PlayerHealth health = other.GetComponentInParent<PlayerHealth>();
        if (health == null || health.IsDead)
            return;

        if (overlapping.Add(health) && !contactStartTime.ContainsKey(health))
            contactStartTime[health] = NetworkTime;
    }

    private void UnregisterContact(Collider other)
    {
        if (other == null)
            return;

        PlayerHealth health = other.GetComponentInParent<PlayerHealth>();
        if (health == null)
            return;

        overlapping.Remove(health);
        contactStartTime.Remove(health);
    }

    private void PruneDestroyed()
    {
        if (overlapping.Count == 0 && contactStartTime.Count == 0)
            return;

        pruneBuffer.Clear();
        foreach (PlayerHealth health in overlapping)
        {
            if (health == null)
                pruneBuffer.Add(health);
        }

        for (int i = 0; i < pruneBuffer.Count; i++)
            overlapping.Remove(pruneBuffer[i]);

        pruneBuffer.Clear();
        foreach (KeyValuePair<PlayerHealth, double> pair in contactStartTime)
        {
            if (pair.Key == null)
                pruneBuffer.Add(pair.Key);
        }

        for (int i = 0; i < pruneBuffer.Count; i++)
            contactStartTime.Remove(pruneBuffer[i]);
    }
}
