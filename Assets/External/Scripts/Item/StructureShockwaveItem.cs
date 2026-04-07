using UnityEngine;

public class StructureShockwaveItem : ItemBase
{
    public override void Activate(GameObject user)
    {
        ShockwaveAbility ability = user.GetComponent<ShockwaveAbility>();

        if (ability != null)
        {
            ability.Execute();
        }
    }
}