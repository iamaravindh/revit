# BIM Intelligence - Revit Plugin

A Revit 2027 plugin that extracts room data and integrates an AI-powered chatbot for querying the building model in real-time using natural language.

## Features

### Room Data Extractor (Ribbon Command)
- Extracts all rooms from the currently open Revit model
- Displays data in a sortable WPF DataGrid: Room Name, Number, Level, Area (m2), Doors, Windows
- Export to JSON with one click
- Filters out unplaced/unbounded rooms automatically

### AI Chatbot (Dockable Panel)
- Dockable WPF chat panel inside Revit
- Powered by Anthropic Claude with tool calling
- Fetches **live data** from the active model at runtime (not pre-exported)
- Three extraction tools for comprehensive coverage:
  - `extract_room_data` - Detailed per-room data (name, number, level, area, doors, windows)
  - `extract_model_info` - Full model summary with dynamic category scanning (captures ALL Revit element categories automatically)
  - `extract_current_view` - Active view info, visible elements, sheets, and views list
- Retry logic with exponential backoff for API reliability

### Example Questions
- "How many rooms are on Level 2?"
- "Which level has the most doors?"
- "List all rooms with area less than 20 sqm"
- "What is the average room size on Level 1?"
- "Which rooms have no windows?"
- "Give me a summary of the building model"
- "What am I currently looking at?"

## Prerequisites

- **Autodesk Revit 2027**
- **.NET 10.0 SDK**
- **Anthropic API Key** - get one at https://console.anthropic.com

## Setup Instructions

### 1. Clone the repository

```bash
git clone https://github.com/iamaravindh/revit.git
cd revit
```

### 2. Set your API key

Edit `BIMIntelligence/appsettings.json`:
```json
{
  "AnthropicApiKey": "your-api-key-here"
}
```

### 3. Build and publish

```bash
cd BIMIntelligence
dotnet publish -c Release -o "%APPDATA%\Autodesk\Revit\Addins\2027\BIMIntelligence"
```

### 4. Install the add-in manifest

Copy `BIMIntelligence.addin` to:
```
%APPDATA%\Autodesk\Revit\Addins\2027\
```

Update the `<Assembly>` path in the `.addin` file:
```xml
<Assembly>C:\Users\<YourUsername>\AppData\Roaming\Autodesk\Revit\Addins\2027\BIMIntelligence\BIMIntelligence.dll</Assembly>
```

### 5. Launch Revit

1. Open Revit 2027
2. Click "Always Load" when prompted about the add-in
3. Open a sample file (e.g., Snowdon Towers Sample Architectural.rvt)
4. Navigate to the **BIM Intelligence** ribbon tab
5. Click **Extract Rooms** to view and export room data
6. Click **AI Chat** to open the chatbot panel

## Architecture

```
BIMIntelligence/
├── App.cs                              # Plugin entry point (IExternalApplication)
├── Commands/
│   ├── RoomDataExtractorCommand.cs     # Ribbon button for room extraction
│   └── ToggleChatCommand.cs            # Toggle chat panel visibility
├── Services/
│   ├── ChatService.cs                  # Anthropic Claude API with tool calling + retry logic
│   ├── RoomDataService.cs             # Revit API queries (rooms, levels, model summary, active view)
│   └── DataExtractionHandler.cs        # ExternalEvent handler for thread-safe Revit API access
├── Models/
│   ├── RoomData.cs                     # Data models (RoomData, LevelData, ModelSummary, ActiveViewData, etc.)
│   └── ChatMessage.cs                  # Chat message model
├── Views/
│   ├── RoomDataPanel.xaml/.cs          # WPF DataGrid for room data display + JSON export
│   └── ChatPanel.xaml/.cs              # Dockable WPF chat panel
├── appsettings.json                    # API key configuration
└── BIMIntelligence.addin               # Revit add-in manifest
```

### How the Chatbot Works

1. User types a question in the chat panel
2. The question is sent to Claude with tool definitions
3. Claude determines which extraction tool to call (rooms, model info, or current view)
4. The plugin runs the Revit API query on Revit's main thread via `ExternalEvent`
5. The live data (JSON) is sent back to Claude as the tool result
6. Claude analyzes the data and responds in natural language

### Key Design Decisions

- **ExternalEvent pattern** for thread safety - WPF chat runs on its own thread, Revit API requires the main thread. The `DataExtractionHandler` bridges these two threads safely.
- **Dynamic category scanning** - Instead of hardcoding Revit categories, the plugin scans ALL elements and groups by category name. This automatically supports architectural, structural, MEP, electrical, plumbing, and any future category types.
- **Tool calling** - The LLM uses Anthropic's tool calling to determine which data extraction to run, then the plugin executes the Revit API query and returns results for natural language response.
- **Retry logic** - Exponential backoff (up to 5 attempts) for API overloaded/rate-limited responses.
- **Area conversion** - Revit stores area in square feet internally; converted to square meters for display.

## Sample File

The `rac_advanced_sample_project.rvt` is not included in Revit 2027. This plugin was tested with **Snowdon Towers Sample Architectural.rvt** located at:
```
C:\Program Files\Autodesk\Revit 2027\Samples\Snowdon Towers Sample Architectural.rvt
```

## Assumptions

- Revit 2027 uses .NET 10.0 (confirmed via Revit's runtime config)
- Room areas are converted from Revit's internal square feet to square meters
- Level elevations are converted from feet to meters
- Only placed/bounded rooms (Area > 0) are included in extraction
- Doors and windows are linked to rooms via `FromRoom`/`ToRoom` properties on `FamilyInstance`
- The chatbot uses Claude 3 Haiku for fast responses (can be switched to Sonnet/Opus in ChatService.cs)

## Tech Stack

- **C# / .NET 10.0** - target framework matching Revit 2027
- **WPF** - UI panels (DataGrid for rooms, dockable chat panel)
- **Revit API** - FilteredElementCollector, Room, FamilyInstance, Level, View, ViewSheet
- **Anthropic Claude API** - LLM with tool calling for the chatbot
- **Newtonsoft.Json** - JSON serialization
