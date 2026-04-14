using System.Dynamic;
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
    /// Lightweight extraction for chatbot — key fields only, capped at 500 elements.
    /// Returns a JSON-friendly summary grouped by level.
    /// </summary>
    public static CategoryChatData ExtractCategoryForChat(Document doc, string categoryName)
    {
        var elements = new FilteredElementCollector(doc)
            .WhereElementIsNotElementType()
            .ToElements()
            .Where(e => e.Category?.Name == categoryName)
            .ToList();

        var data = new CategoryChatData
        {
            CategoryName = categoryName,
            TotalCount = elements.Count
        };

        // Group by level
        var byLevel = new Dictionary<string, List<CategoryElementSummary>>();
        var capped = elements.Take(500).ToList();

        foreach (var elem in capped)
        {
            // Get level
            var levelParam = elem.get_Parameter(BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM)
                          ?? elem.get_Parameter(BuiltInParameter.FAMILY_LEVEL_PARAM)
                          ?? elem.get_Parameter(BuiltInParameter.SCHEDULE_LEVEL_PARAM)
                          ?? elem.get_Parameter(BuiltInParameter.RBS_START_LEVEL_PARAM);
            string levelName = "Unknown";
            if (levelParam != null && levelParam.StorageType == StorageType.ElementId)
            {
                var levelId = levelParam.AsElementId();
                levelName = doc.GetElement(levelId)?.Name ?? "Unknown";
            }
            else if (levelParam != null)
            {
                levelName = levelParam.AsValueString() ?? "Unknown";
            }

            var family = elem.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM)?.AsValueString() ?? "";
            var type = elem.get_Parameter(BuiltInParameter.ELEM_TYPE_PARAM)?.AsValueString() ?? "";

            // Get a few key dimension/size parameters
            var size = elem.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM)?.AsValueString()
                    ?? elem.get_Parameter(BuiltInParameter.DOOR_WIDTH)?.AsValueString()
                    ?? elem.get_Parameter(BuiltInParameter.WINDOW_WIDTH)?.AsValueString()
                    ?? "";

            if (!byLevel.ContainsKey(levelName))
                byLevel[levelName] = new List<CategoryElementSummary>();

            byLevel[levelName].Add(new CategoryElementSummary
            {
                Family = family,
                Type = type,
                Size = size
            });
        }

        // Summarize per level: count by family+type
        foreach (var (level, elems) in byLevel)
        {
            var grouped = elems.GroupBy(e => $"{e.Family}|{e.Type}|{e.Size}")
                .Select(g =>
                {
                    var first = g.First();
                    return new CategoryLevelGroup
                    {
                        Family = first.Family,
                        Type = first.Type,
                        Size = first.Size,
                        Count = g.Count()
                    };
                })
                .OrderByDescending(g => g.Count)
                .ToList();

            data.ByLevel[level] = new CategoryLevelSummary
            {
                Count = elems.Count,
                Types = grouped
            };
        }

        return data;
    }

    /// <summary>
    /// Dynamically extracts ALL parameters for elements in a given category.
    /// Returns column names and rows as ExpandoObjects for WPF DataGrid binding.
    /// </summary>
    public static (List<string> columns, List<ExpandoObject> rows) ExtractCategoryElements(Document doc, string categoryName)
    {
        var elements = new FilteredElementCollector(doc)
            .WhereElementIsNotElementType()
            .ToElements()
            .Where(e => e.Category?.Name == categoryName)
            .ToList();

        // Synthetic columns always come first
        var syntheticColumns = new List<string> { "Element Id", "Family", "Type", "Level" };
        var paramColumns = new HashSet<string>();
        var rows = new List<ExpandoObject>();

        foreach (var elem in elements)
        {
            dynamic row = new ExpandoObject();
            var dict = (IDictionary<string, object?>)row;

            // Synthetic values
            dict["Element Id"] = elem.Id.Value;
            dict["Family"] = elem.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM)?.AsValueString() ?? "";
            dict["Type"] = elem.get_Parameter(BuiltInParameter.ELEM_TYPE_PARAM)?.AsValueString() ?? "";

            // Level
            var levelParam = elem.get_Parameter(BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM)
                          ?? elem.get_Parameter(BuiltInParameter.FAMILY_LEVEL_PARAM)
                          ?? elem.get_Parameter(BuiltInParameter.SCHEDULE_LEVEL_PARAM);
            if (levelParam != null && levelParam.StorageType == StorageType.ElementId)
            {
                var levelId = levelParam.AsElementId();
                dict["Level"] = doc.GetElement(levelId)?.Name ?? "";
            }
            else
            {
                dict["Level"] = levelParam?.AsValueString() ?? "";
            }

            // Read ALL parameters dynamically
            foreach (Parameter param in elem.Parameters)
            {
                var name = param.Definition?.Name;
                if (string.IsNullOrWhiteSpace(name)) continue;
                if (syntheticColumns.Contains(name)) continue; // skip duplicates

                paramColumns.Add(name);
                dict[name] = GetParameterDisplayValue(param, doc);
            }

            rows.Add((ExpandoObject)row);
        }

        // Build final column order: synthetic first, then alphabetical parameter columns
        var sortedParamColumns = paramColumns.OrderBy(c => c).ToList();
        var allColumns = new List<string>(syntheticColumns);
        allColumns.AddRange(sortedParamColumns);

        // Ensure all rows have all columns (fill missing with empty string)
        foreach (var row in rows)
        {
            var dict = (IDictionary<string, object?>)row;
            foreach (var col in allColumns)
            {
                if (!dict.ContainsKey(col))
                    dict[col] = "";
            }
        }

        // Remove columns where ALL values are empty/null
        var populatedColumns = allColumns.Where(col =>
        {
            // Always keep synthetic columns
            if (syntheticColumns.Contains(col)) return true;

            // Check if any row has a non-empty value
            return rows.Any(row =>
            {
                var dict = (IDictionary<string, object?>)row;
                var val = dict.ContainsKey(col) ? dict[col] : null;
                return val != null && val.ToString() != "";
            });
        }).ToList();

        // Remove empty columns from each row too
        var removedColumns = allColumns.Except(populatedColumns).ToHashSet();
        if (removedColumns.Count > 0)
        {
            foreach (var row in rows)
            {
                var dict = (IDictionary<string, object?>)row;
                foreach (var col in removedColumns)
                    dict.Remove(col);
            }
        }

        return (populatedColumns, rows);
    }

    /// <summary>
    /// Gets the display value of a parameter, converting units as needed.
    /// </summary>
    private static object? GetParameterDisplayValue(Parameter param, Document doc)
    {
        if (!param.HasValue) return "";

        switch (param.StorageType)
        {
            case StorageType.String:
                return param.AsString() ?? "";
            case StorageType.Integer:
                // Check if it's a Yes/No parameter
                if (param.Definition is InternalDefinition def)
                {
                    try
                    {
                        var spec = def.GetDataType();
                        if (spec == SpecTypeId.Boolean.YesNo)
                            return param.AsInteger() == 1 ? "Yes" : "No";
                    }
                    catch { }
                }
                return param.AsInteger();
            case StorageType.Double:
                // Use AsValueString for proper unit conversion (shows what user sees in Revit)
                return param.AsValueString() ?? Math.Round(param.AsDouble(), 4).ToString();
            case StorageType.ElementId:
                var id = param.AsElementId();
                if (id == ElementId.InvalidElementId) return "";
                return doc.GetElement(id)?.Name ?? id.Value.ToString();
            default:
                return "";
        }
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
