using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

/// <summary>
/// After a grace period, kills everyone still overlapping in one batch.
/// All current occupants must each complete <see cref="graceSeconds"/> before anyone resolves (no early solo-kill while another is still overlapping).
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

    [Header("Debug")]
    [SerializeField] private bool logTrapEvents = true;
    [SerializeField] private bool logGraceWait;

    private readonly HashSet<PlayerHealth> overlapping = new();
    private readonly Dictionary<PlayerHealth, double> contactStartTime = new();
    private readonly List<PlayerHealth> workList = new();
    private readonly List<PlayerHealth> pruneBuffer = new();
    private readonly List<PlayerHealth> occupantsBuffer = new();
    private double lastGraceWaitLogTime = -1d;

    private static double NetworkTime =>
        PhotonNetwork.InRoom ? PhotonNetwork.Time : Time.timeAsDouble;

    private void LogTrap(string message)
    {
        if (!logTrapEvents)
            return;

        Debug.Log($"[{nameof(InstantKillOnContact)}:{name}] {message}", this);
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
        occupantsBuffer.Clear();

        foreach (PlayerHealth health in overlapping)
        {
            if (health == null || health.IsDead)
                continue;

            if (!contactStartTime.ContainsKey(health))
                continue;

            occupantsBuffer.Add(health);
        }

        if (occupantsBuffer.Count == 0)
            return;

        // Everyone still overlapping must finish their own grace window before anyone dies,
        // so a player who entered earlier cannot resolve alone while another is still "warming up".
        for (int i = 0; i < occupantsBuffer.Count; i++)
        {
            PlayerHealth health = occupantsBuffer[i];
            if (!contactStartTime.TryGetValue(health, out double t0))
                return;

            if (now < t0 + graceSeconds)
            {
                if (logTrapEvents && logGraceWait && (lastGraceWaitLogTime < 0d || now - lastGraceWaitLogTime > 0.35d))
                {
                    lastGraceWaitLogTime = now;
                    LogTrap(
                        $"WaitSynchronizedGrace waitingForActor={health.OwnerActorNumber} " +
                        $"needUntil={t0 + graceSeconds:F3} now={now:F3} occupants={occupantsBuffer.Count}");
                }

                return;
            }
        }

        lastGraceWaitLogTime = -1d;

        workList.Clear();
        for (int i = 0; i < occupantsBuffer.Count; i++)
            workList.Add(occupantsBuffer[i]);

        ResolveBatch(now);
    }

    private void ResolveBatch(double now)
    {
        SortByActorNumber(workList);

        int activatorViewId = -1;
        bool hadActivator = TryGetRecentActivatorViewId(now, out int resolved);
        if (hadActivator)
            activatorViewId = resolved;

        int activatorIndex = -1;
        if (activatorViewId > 0)
        {
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

        bool activatorInBatch = activatorViewId > 0 && activatorIndex >= 0;
        LogTrap(
            $"ResolveBatch now={now:F3} count={workList.Count} " +
            $"activatorViewId={(activatorViewId > 0 ? activatorViewId.ToString() : "none")} " +
            $"activatorInBatch={(activatorViewId > 0 ? (activatorInBatch ? "yes" : "no(outside)") : "n/a")} " +
            $"reorderedFirst={(activatorIndex > 0 ? "yes" : "no")} " +
            $"killOrder=[{BuildKillOrderSummary()}]");

        for (int i = 0; i < workList.Count; i++)
            workList[i].RequestKill();
    }

    private string BuildKillOrderSummary()
    {
        if (workList == null || workList.Count == 0)
            return string.Empty;

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        for (int i = 0; i < workList.Count; i++)
        {
            if (i > 0)
                sb.Append(", ");

            PlayerHealth h = workList[i];
            if (h == null)
            {
                sb.Append("null");
                continue;
            }

            sb.Append("a");
            sb.Append(h.OwnerActorNumber);
            sb.Append(":v");
            sb.Append(GetPlayerViewId(h));
        }

        return sb.ToString();
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
        {
            contactStartTime[health] = NetworkTime;
            LogTrap(
                $"Enter overlap actor={health.OwnerActorNumber} viewId={GetPlayerViewId(health)} " +
                $"t0={contactStartTime[health]:F3} grace={graceSeconds}s overlapCount={overlapping.Count}");
        }
    }

    private void UnregisterContact(Collider other)
    {
        if (other == null)
            return;

        PlayerHealth health = other.GetComponentInParent<PlayerHealth>();
        if (health == null)
            return;

        if (overlapping.Remove(health))
        {
            contactStartTime.Remove(health);
            LogTrap($"Exit overlap actor={health.OwnerActorNumber} viewId={GetPlayerViewId(health)} overlapCount={overlapping.Count}");
        }
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
