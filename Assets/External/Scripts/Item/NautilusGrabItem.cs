using UnityEngine;

public class NautilusGrabItem : ItemBase
{
    public override void Activate(GameObject user)
    {
        NautilusGrabAbility ability = user.GetComponent<NautilusGrabAbility>();

        if (ability != null)
        {
            ability.StartCoroutine(ability.Execute());
        }
    }
}