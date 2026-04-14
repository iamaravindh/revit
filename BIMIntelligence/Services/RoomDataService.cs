using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using BIMIntelligence.Models;

namespace BIMIntelligence.Services;

public static class RoomDataService
{
    private const double SqFtToSqM = 0.092903;
    private const double FtToM = 0.3048;

    public static List<RoomData> ExtractAll(Document doc)
    {
        var rooms = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_Rooms)
            .WhereElementIsNotElementType()
            .Cast<Room>()
            .Where(r => r.Area > 0)
            .ToList();

        var doorsByRoom = CountElementsByRoom(doc, BuiltInCategory.OST_Doors);
        var windowsByRoom = CountElementsByRoom(doc, BuiltInCategory.OST_Windows);

        var results = new List<RoomData>();
        foreach (var room in rooms)
        {
            var roomId = room.Id;
            results.Add(new RoomData
            {
                RoomName = room.get_Parameter(BuiltInParameter.ROOM_NAME)?.AsString() ?? "Unknown",
                RoomNumber = room.get_Parameter(BuiltInParameter.ROOM_NUMBER)?.AsString() ?? "",
                Level = room.Level?.Name ?? "Unknown",
                AreaSqM = Math.Round(room.Area * SqFtToSqM, 2),
                DoorCount = doorsByRoom.GetValueOrDefault(roomId, 0),
                WindowCount = windowsByRoom.GetValueOrDefault(roomId, 0)
            });
        }

        return results;
    }

    public static List<LevelData> ExtractLevels(Document doc)
    {
        var levels = new FilteredElementCollector(doc)
            .OfClass(typeof(Level))
            .Cast<Level>()
            .OrderBy(l => l.Elevation)
            .ToList();

        return levels.Select(l => new LevelData
        {
            Name = l.Name,
            Elevation = Math.Round(l.Elevation * FtToM, 2)
        }).ToList();
    }

    /// <summary>
    /// Dynamically scans ALL element instances in the model, groups by category name,
    /// and returns counts. This captures every Revit category automatically —
    /// architectural, structural, MEP, electrical, plumbing, etc.
    /// </summary>
    public static ModelSummary ExtractModelSummary(Document doc)
    {
        var summary = new ModelSummary
        {
            ProjectName = doc.Title,
            FilePath = doc.PathName
        };

        // Get ALL element instances in the model (not types)
        var allElements = new FilteredElementCollector(doc)
            .WhereElementIsNotElementType()
            .ToElements();

        var categoryCounts = new Dictionary<string, int>();

        foreach (var elem in allElements)
        {
            // Skip elements without a category
            var cat = elem.Category;
            if (cat == null || string.IsNullOrWhiteSpace(cat.Name))
                continue;

            // Skip internal/system categories (negative IDs are internal)
            if (cat.Id.Value < 0 && !IsUserFacingCategory(cat))
                continue;

            var catName = cat.Name;
            categoryCounts[catName] = categoryCounts.GetValueOrDefault(catName, 0) + 1;
        }

        // Sort by count descending
        summary.CategoryCounts = categoryCounts
            .OrderByDescending(kv => kv.Value)
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        // Levels detail
        summary.Levels = ExtractLevels(doc);
        summary.TotalElements = allElements.Count;

        return summary;
    }

    /// <summary>
    /// Extracts information about the currently active view, elements visible in it,
    /// and lists all sheets/views in the model.
    /// </summary>
    public static ActiveViewData ExtractActiveView(Autodesk.Revit.UI.UIDocument uidoc)
    {
        var doc = uidoc.Document;
        var view = uidoc.ActiveView;

        var data = new ActiveViewData
        {
            ViewName = view.Name,
            ViewType = view.ViewType.ToString(),
            LevelName = view.GenLevel?.Name ?? "N/A",
            Scale = view.Scale,
            DetailLevel = view.DetailLevel.ToString()
        };

        // Elements visible in the current view
        var visibleElements = new FilteredElementCollector(doc, view.Id)
            .WhereElementIsNotElementType()
            .ToElements();

        var viewCategoryCounts = new Dictionary<string, int>();
        foreach (var elem in visibleElements)
        {
            var cat = elem.Category;
            if (cat == null || string.IsNullOrWhiteSpace(cat.Name)) continue;
            viewCategoryCounts[cat.Name] = viewCategoryCounts.GetValueOrDefault(cat.Name, 0) + 1;
        }

        data.VisibleElementCount = visibleElements.Count;
        data.VisibleCategoryCounts = viewCategoryCounts
            .OrderByDescending(kv => kv.Value)
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        // All sheets in the model
        var sheets = new FilteredElementCollector(doc)
            .OfClass(typeof(ViewSheet))
            .Cast<ViewSheet>()
            .OrderBy(s => s.SheetNumber)
            .ToList();

        data.Sheets = sheets.Select(s => new SheetInfo
        {
            SheetNumber = s.SheetNumber,
            SheetName = s.Name
        }).ToList();

        // All views in the model
        var views = new FilteredElementCollector(doc)
            .OfClass(typeof(View))
            .Cast<View>()
            .Where(v => !v.IsTemplate)
            .OrderBy(v => v.ViewType.ToString())
            .ThenBy(v => v.Name)
            .ToList();

        data.Views = views.Select(v => new ViewInfo
        {
            Name = v.Name,
            ViewType = v.ViewType.ToString(),
            LevelName = v.GenLevel?.Name ?? ""
        }).ToList();

        return data;
    }

    /// <summary>
    /// Checks if a built-in category is user-facing (visible in schedules/browsers).
    /// We include all categories that have a valid CategoryType of Model or Annotation.
    /// </summary>
    private static bool IsUserFacingCategory(Category cat)
    {
        try
        {
            return cat.CategoryType == CategoryType.Model
                || cat.CategoryType == CategoryType.AnalyticalModel;
        }
        catch
        {
            return false;
        }
    }

    private static Dictionary<ElementId, int> CountElementsByRoom(Document doc, BuiltInCategory category)
    {
        var countByRoom = new Dictionary<ElementId, int>();

        var elements = new FilteredElementCollector(doc)
            .OfCategory(category)
            .WhereElementIsNotElementType()
            .Cast<FamilyInstance>();

        foreach (var instance in elements)
        {
            if (instance.FromRoom != null)
            {
                var id = instance.FromRoom.Id;
                countByRoom[id] = countByRoom.GetValueOrDefault(id, 0) + 1;
            }
            if (instance.ToRoom != null)
            {
                var id = instance.ToRoom.Id;
                countByRoom[id] = countByRoom.GetValueOrDefault(id, 0) + 1;
            }
        }

        return countByRoom;
    }
}
