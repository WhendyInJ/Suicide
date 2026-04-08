public enum ItemUseMode
{
    Instant = 0,
    Aimed = 1
}
public enum ItemAimType
{
    None = 0,
    WorldPoint = 1,
    WorldDirection = 2,
    Target = 3
}
public interface IItemPickupReceiver
{
    int PickupReceiverViewId { get; }
    bool HasLocalPickupAuthority { get; }

    bool CanReceivePickup(ItemDefinition definition, int amount);
    bool ReceivePickup(ItemDefinition definition, int amount);
}