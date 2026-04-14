using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using BIMIntelligence.Models;

namespace BIMIntelligence.Services;

/// <summary>
/// Extracts room-specific data from the Revit model.
/// Used by the "Extract Rooms" ribbon command and the extract_room_data chatbot tool.
/// </summary>
public static class RoomDataService
{
    private const double SqFtToSqM = 0.092903;

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

            // Get level name — try direct property first, then parameter lookup
            var levelName = room.Level?.Name;
            if (string.IsNullOrEmpty(levelName))
            {
                var levelParam = room.get_Parameter(BuiltInParameter.ROOM_LEVEL_ID);
                if (levelParam != null && levelParam.StorageType == StorageType.ElementId)
                {
                    var levelId = levelParam.AsElementId();
                    levelName = doc.GetElement(levelId)?.Name;
                }
            }
            if (string.IsNullOrEmpty(levelName))
            {
                levelName = room.get_Parameter(BuiltInParameter.LEVEL_NAME)?.AsString();
            }

            results.Add(new RoomData
            {
                RoomName = room.get_Parameter(BuiltInParameter.ROOM_NAME)?.AsString() ?? "Unknown",
                RoomNumber = room.get_Parameter(BuiltInParameter.ROOM_NUMBER)?.AsString() ?? "",
                Level = levelName ?? "Unknown",
                AreaSqM = Math.Round(room.Area * SqFtToSqM, 2),
                DoorCount = doorsByRoom.GetValueOrDefault(roomId, 0),
                WindowCount = windowsByRoom.GetValueOrDefault(roomId, 0)
            });
        }

        return results;
    }

    internal static Dictionary<ElementId, int> CountElementsByRoom(Document doc, BuiltInCategory category)
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
