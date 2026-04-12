# BIM Intelligence — Revit Plugin

A Revit 2027 plugin that extracts room data and provides an AI-powered chatbot for querying the building model in real-time using natural language.

## Features

### Room Data Extractor (Ribbon Command)
- Extracts all rooms from the currently open Revit model
- Displays data in a sortable WPF DataGrid: Room Name, Number, Level, Area (m²), Doors, Windows
- Exports extracted data to a JSON file

### AI Chatbot (Dockable Panel)
- Dockable chat panel inside Revit for natural language queries
- Powered by Anthropic Claude with tool calling
- Fetches **live data** from the model at runtime (not pre-exported)
- Example questions:
  - "How many rooms are on Level 2?"
  - "Which level has the most doors?"
  - "List all rooms with area less than 20 sqm"
  - "What is the average room size on Level 1?"
  - "Which rooms have no windows?"

## Prerequisites

- **Autodesk Revit 2027** (installed)
- **Visual Studio 2022** (Community edition, free)
- **.NET 8 SDK** (should come with VS 2022)
- **Anthropic API Key** — get one at https://console.anthropic.com

## Setup Instructions

### 1. Clone the repository

```bash
git clone https://github.com/iamaravindh/revit.git
cd revit
```

### 2. Open in Visual Studio

Open `BIMIntelligence.sln` in Visual Studio 2022.

### 3. Verify Revit API references

The project references `RevitAPI.dll` and `RevitAPIUI.dll` from:
```
C:\Program Files\Autodesk\Revit 2027\
```
If your Revit is installed elsewhere, update the paths in `BIMIntelligence.csproj`.

### 4. Build the project

Build in **Release** or **Debug** mode. The output DLL will be in:
```
BIMIntelligence\bin\Release\net8.0-windows\
```

### 5. Set your API key

Edit `BIMIntelligence\bin\Release\net8.0-windows\appsettings.json`:
```json
{
  "AnthropicApiKey": "sk-ant-your-key-here"
}
```

### 6. Install the plugin into Revit

Copy the `.addin` manifest file to Revit's addins folder:
```
C:\Users\<YourUsername>\AppData\Roaming\Autodesk\Revit\Addins\2027\
```

Edit the copied `BIMIntelligence.addin` file and set the `<Assembly>` path to point to your built DLL:
```xml
<Assembly>C:\path\to\your\bin\Release\net8.0-windows\BIMIntelligence.dll</Assembly>
```

### 7. Launch Revit

1. Open Revit 2027
2. Open the sample file: `C:\Users\Public\Documents\Autodesk\RVT 2027\rac_advanced_sample_project.rvt`
3. You'll see a **"BIM Intelligence"** tab in the ribbon
4. Click **"Extract Rooms"** to extract and view room data
5. Click **"AI Chat"** to open the chatbot panel

## Architecture

```
BIMIntelligence/
├── App.cs                          — Plugin entry point (IExternalApplication)
├── Commands/
│   ├── RoomDataExtractorCommand.cs — Ribbon button command
│   └── ToggleChatCommand.cs        — Toggle chat panel visibility
├── Models/
│   ├── RoomData.cs                 — Room data model
│   └── ChatMessage.cs              — Chat message model
├── Services/
│   ├── RoomDataService.cs          — Revit API queries (FilteredElementCollector)
│   ├── ChatService.cs              — Anthropic Claude API with tool calling
│   └── DataExtractionHandler.cs    — ExternalEvent handler (thread bridge)
└── Views/
    ├── RoomDataPanel.xaml/.cs      — Data grid window with export
    └── ChatPanel.xaml/.cs          — Dockable chat panel
```

### How the Chatbot Works

1. User types a question in the chat panel
2. The question is sent to Claude with a `extract_room_data` tool definition
3. Claude decides to call the tool
4. The plugin runs `RoomDataService.ExtractAll()` on Revit's main thread via `ExternalEvent`
5. The live room data (JSON) is sent back to Claude as the tool result
6. Claude analyzes the data and responds in natural language

### Key Design Decisions

- **ExternalEvent pattern** for thread safety — WPF runs on its own thread, Revit API requires the main thread
- **Single tool with full extraction** — simpler than parameterized queries; Claude filters/aggregates the data itself
- **Area converted to square meters** — Revit stores area in square feet internally
- **Rooms with Area > 0 only** — filters out unplaced/unbounded room objects

## Assumptions

- Revit 2027 targets .NET 8 (based on presence of `.deps.json` files in install directory)
- The sample file `rac_advanced_sample_project.rvt` is used for testing
- Doors and windows are linked to rooms via `FromRoom`/`ToRoom` properties on `FamilyInstance`
- The Anthropic API key is stored in a local config file (not committed to git)

## Tech Stack

- **C# / .NET 8** — target framework matching Revit 2027
- **WPF** — UI framework for panels inside Revit
- **Revit API** — `FilteredElementCollector`, `Room`, `FamilyInstance`, `BuiltInCategory`
- **Anthropic Claude API** — LLM with tool calling for the chatbot
- **Newtonsoft.Json** — JSON serialization for export and API communication
