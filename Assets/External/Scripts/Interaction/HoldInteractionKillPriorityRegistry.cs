using UnityEngine;

/// <summary>
/// Stores the PhotonView.ViewID of the player who last completed a linked hold interaction (e.g. lever).
/// Assign the same instance from <see cref="LeverHoldInteraction"/> and <see cref="InstantKillOnContact"/>.
/// </summary>
[DisallowMultipleComponent]
public class HoldInteractionKillPriorityRegistry : MonoBehaviour
{
    [SerializeField] private int lastCompletingPlayerViewId = -1;

    public int LastCompletingPlayerViewId => lastCompletingPlayerViewId;

    public void SetCompletingPlayerViewId(int playerViewId)
    {
        lastCompletingPlayerViewId = playerViewId > 0 ? playerViewId : -1;
    }
}
