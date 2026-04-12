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
