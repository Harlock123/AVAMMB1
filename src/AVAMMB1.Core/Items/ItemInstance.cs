namespace AVAMMB1.Core.Items;

/// <summary>A concrete item carried by a character.</summary>
public sealed class ItemInstance
{
    /// <summary>Creates an empty instance (for serialization).</summary>
    public ItemInstance()
    {
    }

    /// <summary>Creates an instance of an item definition.</summary>
    /// <param name="itemId">Item definition id.</param>
    /// <param name="charges">Remaining charges, if any.</param>
    public ItemInstance(string itemId, int charges = 0)
    {
        ItemId = itemId;
        Charges = charges;
    }

    /// <summary>Item definition id.</summary>
    public string ItemId { get; set; } = "";

    /// <summary>Remaining charges for wands and similar items.</summary>
    public int Charges { get; set; }
}
