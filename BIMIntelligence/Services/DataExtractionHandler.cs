using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMIntelligence.Models;

namespace BIMIntelligence.Services;

/// <summary>
/// ExternalEvent handler that runs data extraction on Revit's main thread.
/// The chatbot triggers this via ExternalEvent.Raise(), and Revit calls Execute()
/// on its own thread where API calls are safe.
/// </summary>
public class DataExtractionHandler : IExternalEventHandler
{
    /// <summary>
    /// Which tool to execute. Set before calling Raise().
    /// </summary>
    public string ToolName { get; set; } = "extract_room_data";

    /// <summary>
    /// Callback to deliver the extracted data as JSON string.
    /// </summary>
    public Action<string>? OnDataExtracted { get; set; }

    /// <summary>
    /// Callback for errors during extraction.
    /// </summary>
    public Action<string>? OnError { get; set; }

    public void Execute(UIApplication app)
    {
        try
        {
            var doc = app.ActiveUIDocument?.Document;
            if (doc == null)
            {
                OnError?.Invoke("No document is currently open in Revit.");
                return;
            }

            string result;
            switch (ToolName)
            {
                case "extract_room_data":
                    var roomData = RoomDataService.ExtractAll(doc);
                    result = Newtonsoft.Json.JsonConvert.SerializeObject(roomData);
                    break;

                case "extract_model_info":
                    var summary = RoomDataService.ExtractModelSummary(doc);
                    result = Newtonsoft.Json.JsonConvert.SerializeObject(summary);
                    break;

                default:
                    OnError?.Invoke($"Unknown tool: {ToolName}");
                    return;
            }

            OnDataExtracted?.Invoke(result);
        }
        catch (Exception ex)
        {
            OnError?.Invoke($"Extraction failed: {ex.Message}");
        }
    }

    public string GetName() => "BIMIntelligence.DataExtractionHandler";
}
