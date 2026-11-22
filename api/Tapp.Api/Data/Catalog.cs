namespace Tapp.Api.Data;

// Whole numbers only: bottles, pieces, ml. Avoids float drift and keeps SQL arithmetic exact.
public class StockItem
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Unit { get; set; } = "";
    public int Quantity { get; set; }
}

// How much of a stock item is deducted. Used on products (default) and on options (added on top).
public class StockAmount
{
    public int StockItemId { get; set; }
    public int Amount { get; set; }
}

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string? PicturePath { get; set; }
    // shortcut: category is plain text, not a table, move to a table if categories need their own settings
    public string Category { get; set; } = "";
    public int PriceCents { get; set; }
    public bool Is18Plus { get; set; }
    public bool IsHidden { get; set; }
    public List<StockAmount> Stock { get; set; } = [];
    public List<OptionGroup> OptionGroups { get; set; } = [];
}

// A guest picks exactly one option per group.
public class OptionGroup
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public List<Option> Options { get; set; } = [];
}

public class Option
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public List<StockAmount> Stock { get; set; } = [];
}
