using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMIntelligence.Models;

namespace BIMIntelligence.Services;

/// <summary>
/// ExternalEvent handler that runs RoomDataService on Revit's main thread.
/// The chatbot triggers this via ExternalEvent.Raise(), and Revit calls Execute()
/// on its own thread where API calls are safe.
/// </summary>
public class DataExtractionHandler : IExternalEventHandler
{
    /// <summary>
    /// Callback to deliver the extracted data back to the caller (chat panel).
    /// Set this before calling ExternalEvent.Raise().
    /// </summary>
    public Action<List<RoomData>>? OnDataExtracted { get; set; }

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

            var roomData = RoomDataService.ExtractAll(doc);
            OnDataExtracted?.Invoke(roomData);
        }
        catch (Exception ex)
        {
            OnError?.Invoke($"Extraction failed: {ex.Message}");
        }
    }

    public string GetName() => "BIMIntelligence.DataExtractionHandler";
}
