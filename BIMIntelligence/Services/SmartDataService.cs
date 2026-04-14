using System.Dynamic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using BIMIntelligence.Models;

namespace BIMIntelligence.Services;

/// <summary>
/// Smart data extraction service — handles model summaries, active views,
/// dynamic category extraction, and relationship mapping.
/// Used by the "Smart Extract" ribbon command and chatbot tools:
/// extract_model_info, extract_current_view, extract_category_data, extract_relationships.
/// </summary>
public static class SmartDataService
{
    private const double FtToM = 0.3048;

    // ──────────────────────────────────────────────
    // Levels
    // ──────────────────────────────────────────────

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

    // ──────────────────────────────────────────────
    // Model Summary (extract_model_info)
    // ──────────────────────────────────────────────

    /// <summary>
    /// Dynamically scans ALL element instances in the model, groups by category name,
    /// and returns counts. Captures every Revit category automatically.
    /// </summary>
    public static ModelSummary ExtractModelSummary(Document doc)
    {
        var summary = new ModelSummary
        {
            ProjectName = doc.Title,
            FilePath = doc.PathName
        };

        var allElements = new FilteredElementCollector(doc)
            .WhereElementIsNotElementType()
            .ToElements();

        var categoryCounts = new Dictionary<string, int>();
        foreach (var elem in allElements)
        {
            var cat = elem.Category;
            if (cat == null || string.IsNullOrWhiteSpace(cat.Name)) continue;
            if (cat.Id.Value < 0 && !IsUserFacingCategory(cat)) continue;

            categoryCounts[cat.Name] = categoryCounts.GetValueOrDefault(cat.Name, 0) + 1;
        }

        summary.CategoryCounts = categoryCounts
            .OrderByDescending(kv => kv.Value)
            .ToDictionary(kv => kv.Key, kv => kv.Value);
        summary.Levels = ExtractLevels(doc);
        summary.TotalElements = allElements.Count;

        return summary;
    }

    // ──────────────────────────────────────────────
    // Active View (extract_current_view)
    // ──────────────────────────────────────────────

    /// <summary>
    /// Extracts the currently active view, visible elements, sheets, and views.
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

        data.Sheets = new FilteredElementCollector(doc)
            .OfClass(typeof(ViewSheet))
            .Cast<ViewSheet>()
            .OrderBy(s => s.SheetNumber)
            .Select(s => new SheetInfo { SheetNumber = s.SheetNumber, SheetName = s.Name })
            .ToList();

        data.Views = new FilteredElementCollector(doc)
            .OfClass(typeof(View))
            .Cast<View>()
            .Where(v => !v.IsTemplate)
            .OrderBy(v => v.ViewType.ToString())
            .ThenBy(v => v.Name)
            .Select(v => new ViewInfo
            {
                Name = v.Name,
                ViewType = v.ViewType.ToString(),
                LevelName = v.GenLevel?.Name ?? ""
            })
            .ToList();

        return data;
    }

    // ──────────────────────────────────────────────
    // Category Chat Data (extract_category_data)
    // ──────────────────────────────────────────────

    /// <summary>
    /// Lightweight extraction for chatbot — key fields only, capped at 500 elements.
    /// Groups by level with family/type/size breakdown.
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

        var byLevel = new Dictionary<string, List<CategoryElementSummary>>();
        foreach (var elem in elements.Take(500))
        {
            var levelName = GetElementLevel(elem, doc);
            var family = elem.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM)?.AsValueString() ?? "";
            var type = elem.get_Parameter(BuiltInParameter.ELEM_TYPE_PARAM)?.AsValueString() ?? "";
            var size = elem.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM)?.AsValueString()
                    ?? elem.get_Parameter(BuiltInParameter.DOOR_WIDTH)?.AsValueString()
                    ?? elem.get_Parameter(BuiltInParameter.WINDOW_WIDTH)?.AsValueString()
                    ?? "";

            if (!byLevel.ContainsKey(levelName))
                byLevel[levelName] = new List<CategoryElementSummary>();

            byLevel[levelName].Add(new CategoryElementSummary { Family = family, Type = type, Size = size });
        }

        foreach (var (level, elems) in byLevel)
        {
            data.ByLevel[level] = new CategoryLevelSummary
            {
                Count = elems.Count,
                Types = elems.GroupBy(e => $"{e.Family}|{e.Type}|{e.Size}")
                    .Select(g => new CategoryLevelGroup
                    {
                        Family = g.First().Family,
                        Type = g.First().Type,
                        Size = g.First().Size,
                        Count = g.Count()
                    })
                    .OrderByDescending(g => g.Count)
                    .ToList()
            };
        }

        return data;
    }

    // ──────────────────────────────────────────────
    // Relationships (extract_relationships)
    // ──────────────────────────────────────────────

    /// <summary>
    /// Maps how elements relate: host relationships, room containment, and component connections.
    /// </summary>
    public static ModelRelationshipData ExtractRelationships(Document doc)
    {
        var data = new ModelRelationshipData();

        var allInstances = new FilteredElementCollector(doc)
            .WhereElementIsNotElementType()
            .ToElements()
            .OfType<FamilyInstance>()
            .ToList();

        foreach (var instance in allInstances)
        {
            var childCat = instance.Category?.Name;
            if (string.IsNullOrEmpty(childCat)) continue;

            // Host relationship (Door hosted by Wall, Light hosted by Ceiling)
            if (instance.Host != null)
            {
                var hostCat = instance.Host.Category?.Name ?? "Unknown";
                var key = $"{hostCat} → {childCat}";

                if (!data.HostRelationships.ContainsKey(key))
                    data.HostRelationships[key] = new RelationshipInfo { Parent = hostCat, Child = childCat };
                data.HostRelationships[key].Count++;

                var hostId = instance.Host.Id.Value.ToString();
                if (!data.HostRelationships[key].Examples.ContainsKey(hostId))
                    data.HostRelationships[key].Examples[hostId] = new RelationshipExample
                    {
                        ParentName = instance.Host.Name ?? "",
                        ChildCount = 0
                    };
                data.HostRelationships[key].Examples[hostId].ChildCount++;
            }

            // Room containment (Furniture in Room, Fixture in Room)
            var room = instance.Room ?? instance.FromRoom;
            if (room != null)
            {
                var roomName = room.get_Parameter(BuiltInParameter.ROOM_NAME)?.AsString() ?? "Unknown";
                var roomNumber = room.get_Parameter(BuiltInParameter.ROOM_NUMBER)?.AsString() ?? "";
                var key = $"Rooms → {childCat}";

                if (!data.RoomRelationships.ContainsKey(key))
                    data.RoomRelationships[key] = new RelationshipInfo { Parent = "Rooms", Child = childCat };
                data.RoomRelationships[key].Count++;

                var roomId = room.Id.Value.ToString();
                if (!data.RoomRelationships[key].Examples.ContainsKey(roomId))
                    data.RoomRelationships[key].Examples[roomId] = new RelationshipExample
                    {
                        ParentName = $"{roomName} ({roomNumber})",
                        ChildCount = 0
                    };
                data.RoomRelationships[key].Examples[roomId].ChildCount++;
            }

            // Component connection (fitting connected to pipe/duct)
            if (instance.SuperComponent != null)
            {
                var parentCat = instance.SuperComponent.Category?.Name ?? "Unknown";
                var key = $"{parentCat} → {childCat}";

                if (!data.ComponentRelationships.ContainsKey(key))
                    data.ComponentRelationships[key] = new RelationshipInfo { Parent = parentCat, Child = childCat };
                data.ComponentRelationships[key].Count++;
            }
        }

        TrimExamples(data.HostRelationships);
        TrimExamples(data.RoomRelationships);
        return data;
    }

    // ──────────────────────────────────────────────
    // Smart Extract — Full Parameter Extraction
    // ──────────────────────────────────────────────

    /// <summary>
    /// Dynamically extracts ALL parameters for elements in a given category.
    /// Auto-detects parent-child relationships and adds count columns.
    /// Returns column names and rows as ExpandoObjects for WPF DataGrid binding.
    /// </summary>
    public static (List<string> columns, List<ExpandoObject> rows) ExtractCategoryElements(Document doc, string categoryName)
    {
        var elements = new FilteredElementCollector(doc)
            .WhereElementIsNotElementType()
            .ToElements()
            .Where(e => e.Category?.Name == categoryName)
            .ToList();

        var syntheticColumns = new List<string> { "Element Id", "Family", "Type", "Level" };
        var paramColumns = new HashSet<string>();
        var rows = new List<ExpandoObject>();

        // Auto-detect relationships
        var elementIds = new HashSet<ElementId>(elements.Select(e => e.Id));
        var hostedCounts = DetectRelatedElements(doc, elements, elementIds);
        foreach (var relCategory in hostedCounts.Keys.OrderBy(k => k))
            syntheticColumns.Add($"{relCategory} Count");

        foreach (var elem in elements)
        {
            dynamic row = new ExpandoObject();
            var dict = (IDictionary<string, object?>)row;

            dict["Element Id"] = elem.Id.Value;
            dict["Family"] = elem.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM)?.AsValueString() ?? "";
            dict["Type"] = elem.get_Parameter(BuiltInParameter.ELEM_TYPE_PARAM)?.AsValueString() ?? "";
            dict["Level"] = GetElementLevel(elem, doc);

            // Relationship counts
            foreach (var relCategory in hostedCounts.Keys.OrderBy(k => k))
                dict[$"{relCategory} Count"] = hostedCounts[relCategory].GetValueOrDefault(elem.Id, 0);

            // All parameters
            foreach (Parameter param in elem.Parameters)
            {
                var name = param.Definition?.Name;
                if (string.IsNullOrWhiteSpace(name) || syntheticColumns.Contains(name)) continue;
                paramColumns.Add(name);
                dict[name] = GetParameterDisplayValue(param, doc);
            }

            rows.Add((ExpandoObject)row);
        }

        // Build columns and fill missing values
        var allColumns = new List<string>(syntheticColumns);
        allColumns.AddRange(paramColumns.OrderBy(c => c));

        foreach (var row in rows)
        {
            var dict = (IDictionary<string, object?>)row;
            foreach (var col in allColumns)
                if (!dict.ContainsKey(col)) dict[col] = "";
        }

        // Remove entirely empty columns
        var populatedColumns = allColumns.Where(col =>
        {
            if (syntheticColumns.Contains(col)) return true;
            return rows.Any(row =>
            {
                var dict = (IDictionary<string, object?>)row;
                var val = dict.ContainsKey(col) ? dict[col] : null;
                return val != null && val.ToString() != "";
            });
        }).ToList();

        var removedColumns = allColumns.Except(populatedColumns).ToHashSet();
        if (removedColumns.Count > 0)
            foreach (var row in rows)
            {
                var dict = (IDictionary<string, object?>)row;
                foreach (var col in removedColumns) dict.Remove(col);
            }

        return (populatedColumns, rows);
    }

    // ──────────────────────────────────────────────
    // Private Helpers
    // ──────────────────────────────────────────────

    private static string GetElementLevel(Element elem, Document doc)
    {
        var levelParam = elem.get_Parameter(BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM)
                      ?? elem.get_Parameter(BuiltInParameter.FAMILY_LEVEL_PARAM)
                      ?? elem.get_Parameter(BuiltInParameter.SCHEDULE_LEVEL_PARAM)
                      ?? elem.get_Parameter(BuiltInParameter.RBS_START_LEVEL_PARAM);

        if (levelParam != null && levelParam.StorageType == StorageType.ElementId)
        {
            var levelId = levelParam.AsElementId();
            return doc.GetElement(levelId)?.Name ?? "Unknown";
        }
        return levelParam?.AsValueString() ?? "Unknown";
    }

    private static object? GetParameterDisplayValue(Parameter param, Document doc)
    {
        if (!param.HasValue) return "";
        switch (param.StorageType)
        {
            case StorageType.String:
                return param.AsString() ?? "";
            case StorageType.Integer:
                if (param.Definition is InternalDefinition def)
                {
                    try
                    {
                        if (def.GetDataType() == SpecTypeId.Boolean.YesNo)
                            return param.AsInteger() == 1 ? "Yes" : "No";
                    }
                    catch { }
                }
                return param.AsInteger();
            case StorageType.Double:
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
    /// Auto-detects hosted/contained elements for parent elements.
    /// Rooms use FromRoom/ToRoom, other elements use Host property.
    /// </summary>
    private static Dictionary<string, Dictionary<ElementId, int>> DetectRelatedElements(
        Document doc, List<Element> parentElements, HashSet<ElementId> parentIds)
    {
        var result = new Dictionary<string, Dictionary<ElementId, int>>();
        if (parentElements.Count == 0) return result;

        bool parentsAreRooms = parentElements[0].Category?.Id.Value == (long)BuiltInCategory.OST_Rooms;

        var allInstances = new FilteredElementCollector(doc)
            .WhereElementIsNotElementType()
            .ToElements()
            .OfType<FamilyInstance>()
            .ToList();

        foreach (var instance in allInstances)
        {
            var childCatName = instance.Category?.Name;
            if (string.IsNullOrEmpty(childCatName)) continue;
            if (instance.Category?.Id == parentElements[0].Category?.Id) continue;

            ElementId? linkedParentId = null;

            if (parentsAreRooms)
            {
                if (instance.FromRoom != null && parentIds.Contains(instance.FromRoom.Id))
                    linkedParentId = instance.FromRoom.Id;
                else if (instance.ToRoom != null && parentIds.Contains(instance.ToRoom.Id))
                    linkedParentId = instance.ToRoom.Id;
                else if (instance.Room != null && parentIds.Contains(instance.Room.Id))
                    linkedParentId = instance.Room.Id;
            }
            else
            {
                var hostId = instance.Host?.Id;
                if (hostId != null && parentIds.Contains(hostId))
                    linkedParentId = hostId;
            }

            if (linkedParentId != null)
            {
                if (!result.ContainsKey(childCatName))
                    result[childCatName] = new Dictionary<ElementId, int>();
                result[childCatName][linkedParentId] =
                    result[childCatName].GetValueOrDefault(linkedParentId, 0) + 1;
            }
        }

        return result.Where(kv => kv.Value.Values.Sum() > 0)
            .ToDictionary(kv => kv.Key, kv => kv.Value);
    }

    private static bool IsUserFacingCategory(Category cat)
    {
        try { return cat.CategoryType == CategoryType.Model || cat.CategoryType == CategoryType.AnalyticalModel; }
        catch { return false; }
    }

    private static void TrimExamples(Dictionary<string, RelationshipInfo> relationships)
    {
        foreach (var rel in relationships.Values)
        {
            if (rel.Examples.Count > 5)
                rel.Examples = rel.Examples
                    .OrderByDescending(e => e.Value.ChildCount)
                    .Take(5)
                    .ToDictionary(e => e.Key, e => e.Value);
        }
    }
}
