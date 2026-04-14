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
