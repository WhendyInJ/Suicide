using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

/// <summary>
/// After a short grace period, kills everyone still overlapping.
/// If a linked lever was recently completed (trap drop), that player is killed first in RPC order, then the rest by actor number.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class InstantKillOnContact : MonoBehaviour
{
    [Header("Timing")]
    [SerializeField, Min(0f)] private float graceSeconds = 0.12f;

    [Header("Lever → trap (who dropped this hazard)")]
    [Tooltip("Levers that complete to drop this kill zone. The most recent Completed hold within the memory window sets 'activator'.")]
    [SerializeField] private NetworkHoldInteractionBase[] leversThatActivateThisTrap;

    [SerializeField, Min(0.05f)] private float activationMemorySeconds = 12f;

    private readonly HashSet<PlayerHealth> overlapping = new();
    private readonly Dictionary<PlayerHealth, double> contactStartTime = new();
    private readonly List<PlayerHealth> workList = new();
    private readonly List<PlayerHealth> pruneBuffer = new();

    private static double NetworkTime =>
        PhotonNetwork.InRoom ? PhotonNetwork.Time : Time.timeAsDouble;

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
        SortByActorNumber(workList);

        int activatorViewId = -1;
        if (TryGetRecentActivatorViewId(now, out int resolved))
            activatorViewId = resolved;

        if (activatorViewId > 0)
        {
            int activatorIndex = -1;
            for (int i = 0; i < workList.Count; i++)
            {
                if (GetPlayerViewId(workList[i]) == activatorViewId)
                {
                    activatorIndex = i;
                    break;
                }
            }

            if (activatorIndex > 0)
            {
                PlayerHealth activator = workList[activatorIndex];
                for (int i = activatorIndex; i > 0; i--)
                    workList[i] = workList[i - 1];

                workList[0] = activator;
            }
        }

        for (int i = 0; i < workList.Count; i++)
            workList[i].RequestKill();
    }

    private bool TryGetRecentActivatorViewId(double now, out int activatorViewId)
    {
        activatorViewId = -1;
        if (leversThatActivateThisTrap == null || leversThatActivateThisTrap.Length == 0)
            return false;

        double bestTime = double.NegativeInfinity;
        for (int i = 0; i < leversThatActivateThisTrap.Length; i++)
        {
            NetworkHoldInteractionBase lever = leversThatActivateThisTrap[i];
            if (lever == null)
                continue;

            double t = lever.LastCompletionNetworkTime;
            if (t < 0d)
                continue;

            if (now - t > activationMemorySeconds)
                continue;

            int vid = lever.LastCompletedInteractorViewId;
            if (vid <= 0)
                continue;

            if (t > bestTime)
            {
                bestTime = t;
                activatorViewId = vid;
            }
        }

        return activatorViewId > 0;
    }

    private static void SortByActorNumber(List<PlayerHealth> list)
    {
        list.Sort(CompareByActorNumber);
    }

    private static int CompareByActorNumber(PlayerHealth a, PlayerHealth b)
    {
        int na = a != null ? a.OwnerActorNumber : int.MaxValue;
        int nb = b != null ? b.OwnerActorNumber : int.MaxValue;
        return na.CompareTo(nb);
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
