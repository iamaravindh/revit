using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMIntelligence.Services;
using BIMIntelligence.Views;

namespace BIMIntelligence.Commands;

[Transaction(TransactionMode.ReadOnly)]
public class SmartExtractCommand : IExternalCommand
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

            // Scan the model for all categories
            var summary = SmartDataService.ExtractModelSummary(doc);

            if (summary.CategoryCounts.Count == 0)
            {
                TaskDialog.Show("Smart Extract", "No elements found in the current model.");
                return Result.Succeeded;
            }

            // Show the smart extract panel
            var panel = new SmartExtractPanel(doc, summary);
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
