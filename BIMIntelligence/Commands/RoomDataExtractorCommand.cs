using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMIntelligence.Services;
using BIMIntelligence.Views;

namespace BIMIntelligence.Commands;

[Transaction(TransactionMode.ReadOnly)]
public class RoomDataExtractorCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        try
        {
            var doc = commandData.Application.ActiveUIDocument?.Document;
            if (doc == null)
            {
                message = "No document is currently open.";
                return Result.Failed;
            }

            // Extract room data
            var roomData = RoomDataService.ExtractAll(doc);

            if (roomData.Count == 0)
            {
                TaskDialog.Show("Room Data Extractor", "No rooms found in the current model.");
                return Result.Succeeded;
            }

            // Show the data in a WPF window
            var panel = new RoomDataPanel();
            panel.LoadData(roomData);
            panel.ShowDialog();

            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            message = ex.Message;
            return Result.Failed;
        }
    }
}
