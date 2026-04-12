using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using BIMIntelligence.Models;

namespace BIMIntelligence.Services;

public static class RoomDataService
{
    private const double SqFtToSqM = 0.092903;

    public static List<RoomData> ExtractAll(Document doc)
    {
        // Step 1: Collect all rooms with area > 0 (skip unplaced/unbounded)
        var rooms = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_Rooms)
            .WhereElementIsNotElementType()
            .Cast<Room>()
            .Where(r => r.Area > 0)
            .ToList();

        // Step 2: Count doors per room
        var doorsByRoom = CountElementsByRoom(doc, BuiltInCategory.OST_Doors);

        // Step 3: Count windows per room
        var windowsByRoom = CountElementsByRoom(doc, BuiltInCategory.OST_Windows);

        // Step 4: Build results
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
