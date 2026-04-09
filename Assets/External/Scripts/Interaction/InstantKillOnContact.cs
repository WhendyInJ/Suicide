using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class InstantKillOnContact : MonoBehaviour
{
    [SerializeField] private HoldInteractionKillPriorityRegistry killPriorityRegistry;

    private readonly List<PlayerHealth> pendingKills = new();
    private bool endOfFrameFlushScheduled;

    private void OnDisable()
    {
        if (pendingKills.Count > 0)
            FlushPendingKillsImmediate();
    }

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

        PlayerHealth health = other.GetComponentInParent<PlayerHealth>();
        if (health == null)
            return;

        if (killPriorityRegistry == null)
        {
            health.RequestKill();
            return;
        }

        EnqueuePendingKill(health);
        ScheduleEndOfFrameFlush();
    }

    private void EnqueuePendingKill(PlayerHealth health)
    {
        for (int i = 0; i < pendingKills.Count; i++)
        {
            if (pendingKills[i] == health)
                return;
        }

        pendingKills.Add(health);
    }

    private void ScheduleEndOfFrameFlush()
    {
        if (endOfFrameFlushScheduled)
            return;

        endOfFrameFlushScheduled = true;
        StartCoroutine(CoFlushKillsEndOfFrame());
    }

    private IEnumerator CoFlushKillsEndOfFrame()
    {
        yield return new WaitForEndOfFrame();
        endOfFrameFlushScheduled = false;
        FlushPendingKillsImmediate();
    }

    private void FlushPendingKillsImmediate()
    {
        if (pendingKills.Count == 0)
            return;

        SortPendingByLeverCompleterFirst();
        for (int i = 0; i < pendingKills.Count; i++)
            pendingKills[i].RequestKill();

        pendingKills.Clear();
    }

    private void SortPendingByLeverCompleterFirst()
    {
        if (pendingKills.Count <= 1 || killPriorityRegistry == null)
            return;

        int priorityId = killPriorityRegistry.LastCompletingPlayerViewId;
        if (priorityId <= 0)
            return;

        pendingKills.Sort((a, b) =>
        {
            bool aMatch = ResolvePlayerViewId(a) == priorityId;
            bool bMatch = ResolvePlayerViewId(b) == priorityId;
            if (aMatch && !bMatch)
                return -1;
            if (!aMatch && bMatch)
                return 1;
            return 0;
        });
    }

    private static int ResolvePlayerViewId(PlayerHealth health)
    {
        if (health == null)
            return -1;

        PhotonView view = health.GetComponent<PhotonView>();
        if (view == null)
            view = health.GetComponentInParent<PhotonView>();

        return view != null ? view.ViewID : -1;
    }
}
