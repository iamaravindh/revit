namespace BIMIntelligence.Models;

public class RoomData
{
    public string RoomName { get; set; } = string.Empty;
    public string RoomNumber { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public double AreaSqM { get; set; }
    public int DoorCount { get; set; }
    public int WindowCount { get; set; }
}

public class LevelData
{
    public string Name { get; set; } = string.Empty;
    public double Elevation { get; set; }
}

public class ModelSummary
{
    public string ProjectName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public int TotalElements { get; set; }
    public List<LevelData> Levels { get; set; } = new();

    /// <summary>
    /// Dynamic dictionary of ALL categories found in the model with their instance counts.
    /// Key = Category name (e.g. "Walls", "Lighting Fixtures", "Conduits"), Value = count.
    /// Sorted by count descending.
    /// </summary>
    public Dictionary<string, int> CategoryCounts { get; set; } = new();
}

public class ActiveViewData
{
    public string ViewName { get; set; } = string.Empty;
    public string ViewType { get; set; } = string.Empty;
    public string LevelName { get; set; } = string.Empty;
    public int Scale { get; set; }
    public string DetailLevel { get; set; } = string.Empty;
    public int VisibleElementCount { get; set; }
    public Dictionary<string, int> VisibleCategoryCounts { get; set; } = new();
    public List<SheetInfo> Sheets { get; set; } = new();
    public List<ViewInfo> Views { get; set; } = new();
}

public class SheetInfo
{
    public string SheetNumber { get; set; } = string.Empty;
    public string SheetName { get; set; } = string.Empty;
}

public class ViewInfo
{
    public string Name { get; set; } = string.Empty;
    public string ViewType { get; set; } = string.Empty;
    public string LevelName { get; set; } = string.Empty;
}

public class CategoryElementData
{
    public string CategoryName { get; set; } = string.Empty;
    public int ElementCount { get; set; }
    public List<string> Columns { get; set; } = new();
    public List<Dictionary<string, object?>> Rows { get; set; } = new();
}

// Lightweight models for chatbot (fast, small payload)
public class CategoryChatData
{
    public string CategoryName { get; set; } = string.Empty;
    public int TotalCount { get; set; }
    public Dictionary<string, CategoryLevelSummary> ByLevel { get; set; } = new();
}

public class CategoryLevelSummary
{
    public int Count { get; set; }
    public List<CategoryLevelGroup> Types { get; set; } = new();
}

public class CategoryLevelGroup
{
    public string Family { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class CategoryElementSummary
{
    public string Family { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
}

// Relationship models for chatbot
public class ModelRelationshipData
{
    /// <summary>Host relationships: Wall hosts Door, Ceiling hosts Light, etc.</summary>
    public Dictionary<string, RelationshipInfo> HostRelationships { get; set; } = new();

    /// <summary>Room containment: Room contains Furniture, Fixtures, etc.</summary>
    public Dictionary<string, RelationshipInfo> RoomRelationships { get; set; } = new();

    /// <summary>Component relationships: Pipe connects to Pipe Fitting, etc.</summary>
    public Dictionary<string, RelationshipInfo> ComponentRelationships { get; set; } = new();
}

public class RelationshipInfo
{
    public string Parent { get; set; } = string.Empty;
    public string Child { get; set; } = string.Empty;
    public int Count { get; set; }
    /// <summary>Top examples: parentId → { parentName, childCount }</summary>
    public Dictionary<string, RelationshipExample> Examples { get; set; } = new();
}

public class RelationshipExample
{
    public string ParentName { get; set; } = string.Empty;
    public int ChildCount { get; set; }
}
