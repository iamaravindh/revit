using System.Reflection;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;
using BIMIntelligence.Services;
using BIMIntelligence.Views;

namespace BIMIntelligence;

public class App : IExternalApplication
{
    public static readonly Guid ChatPaneGuid = new("B8F7E2A1-3C4D-5E6F-8901-ABCDEF123456");

    // Static references for cross-class access (chatbot needs these)
    public static ExternalEvent DataExtractionEvent = null!;
    public static DataExtractionHandler DataExtractionHandler = null!;

    public Result OnStartup(UIControlledApplication application)
    {
        // 1. Create ribbon tab and panel
        var tabName = "BIM Intelligence";
        application.CreateRibbonTab(tabName);
        var panel = application.CreateRibbonPanel(tabName, "Tools");

        var dllPath = Assembly.GetExecutingAssembly().Location;

        // 2. Add "Extract Rooms" button
        var extractBtnData = new PushButtonData(
            "RoomExtractor",
            "Extract\nRooms",
            dllPath,
            "BIMIntelligence.Commands.RoomDataExtractorCommand"
        )
        {
            ToolTip = "Extract room data (name, number, level, area, doors, windows) from the current model"
        };
        panel.AddItem(extractBtnData);

        // 3. Add "AI Chat" button
        var chatBtnData = new PushButtonData(
            "ToggleChat",
            "AI\nChat",
            dllPath,
            "BIMIntelligence.Commands.ToggleChatCommand"
        )
        {
            ToolTip = "Open/close the AI chatbot panel to ask questions about the building model"
        };
        panel.AddItem(chatBtnData);

        // 4. Register the dockable chat pane
        var chatPanel = new ChatPanel();
        var paneId = new DockablePaneId(ChatPaneGuid);
        application.RegisterDockablePane(paneId, "BIM AI Assistant", chatPanel);

        // 5. Initialize ExternalEvent for thread-safe Revit API access from chatbot
        DataExtractionHandler = new DataExtractionHandler();
        DataExtractionEvent = ExternalEvent.Create(DataExtractionHandler);

        return Result.Succeeded;
    }

    public Result OnShutdown(UIControlledApplication application)
    {
        DataExtractionEvent?.Dispose();
        return Result.Succeeded;
    }
}
