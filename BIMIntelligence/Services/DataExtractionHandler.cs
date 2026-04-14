using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMIntelligence.Models;

namespace BIMIntelligence.Services;

/// <summary>
/// ExternalEvent handler that runs data extraction on Revit's main thread.
/// </summary>
public class DataExtractionHandler : IExternalEventHandler
{
    public string ToolName { get; set; } = "extract_room_data";
    public string CategoryFilter { get; set; } = "";
    public Action<string>? OnDataExtracted { get; set; }
    public Action<string>? OnError { get; set; }

    public void Execute(UIApplication app)
    {
        try
        {
            var uidoc = app.ActiveUIDocument;
            if (uidoc == null)
            {
                OnError?.Invoke("No document is currently open in Revit.");
                return;
            }

            var doc = uidoc.Document;
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

                case "extract_current_view":
                    var viewData = RoomDataService.ExtractActiveView(uidoc);
                    result = Newtonsoft.Json.JsonConvert.SerializeObject(viewData);
                    break;

                case "extract_category_data":
                    var catData = RoomDataService.ExtractCategoryForChat(doc, CategoryFilter);
                    result = Newtonsoft.Json.JsonConvert.SerializeObject(catData);
                    break;

                case "extract_relationships":
                    var relData = RoomDataService.ExtractRelationships(doc);
                    result = Newtonsoft.Json.JsonConvert.SerializeObject(relData);
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
