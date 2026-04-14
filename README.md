# BIM Intelligence - Revit Plugin

An AI-powered Revit 2027 plugin that extracts building data and enables natural language querying of BIM models in real-time.

## What It Does

**Three ribbon commands, one intelligent system:**

| Command | Purpose |
|---------|---------|
| **Extract Rooms** | Extracts room data (name, number, level, area, doors, windows) into a WPF DataGrid. Export to JSON or CSV. |
| **Smart Extract** | Scans the entire model, lists all element categories with counts, and lets you extract ANY category with ALL its parameters dynamically. Auto-detects parent-child relationships (e.g., Walls hosting Doors). Export to JSON or CSV. |
| **AI Chat** | Dockable chat panel powered by Claude Sonnet 4 with tool calling. Asks the Revit API in real-time — no pre-exported data. |

## AI Chatbot - 5 Live Tools

The chatbot uses Anthropic's tool calling to determine which extraction to run, executes the Revit API query on the main thread via `ExternalEvent`, and responds in natural language.

| Tool | What It Returns |
|------|-----------------|
| `extract_model_info` | Project name, levels with elevations, total elements, all category counts |
| `extract_current_view` | Active view name/type/level, visible elements, all sheets and views |
| `extract_room_data` | Per-room: name, number, level, area (m2), door count, window count |
| `extract_category_data` | Any category grouped by level with family/type/size breakdown |
| `extract_relationships` | Host relationships (Wall hosts Door), room containment, MEP connections |

### Example Questions
- "How many levels does this building have and what are their elevations?"
- "Which level has the most doors?"
- "List all rooms with area less than 20 sqm"
- "What is the average room size on Level 1?"
- "What plumbing fixtures are installed on Level 4?"
- "What types of pipes are used in this building?"
- "How are the plumbing elements connected to each other?"

## Architecture

```
BIMIntelligence/
├── App.cs                                # Plugin entry point, ribbon setup, ExternalEvent init
├── Commands/
│   ├── RoomDataExtractorCommand.cs       # "Extract Rooms" ribbon command
│   ├── SmartExtractCommand.cs            # "Smart Extract" ribbon command
│   └── ToggleChatCommand.cs              # "AI Chat" ribbon command
├── Services/
│   ├── RoomDataService.cs                # Room-specific extraction (rooms, doors, windows)
│   ├── SmartDataService.cs               # Model summary, views, categories, relationships, dynamic params
│   ├── ChatService.cs                    # Claude API integration with tool calling + retry logic
│   └── DataExtractionHandler.cs          # ExternalEvent handler — thread-safe bridge to Revit API
├── Models/
│   ├── RoomData.cs                       # All data models (Room, Level, ModelSummary, Relationships, etc.)
│   └── ChatMessage.cs                    # Chat message model
├── Views/
│   ├── RoomDataPanel.xaml/.cs            # WPF DataGrid for room data + JSON/CSV export
│   ├── SmartExtractPanel.xaml/.cs        # Two-phase WPF: category picker + dynamic DataGrid
│   └── ChatPanel.xaml/.cs                # Dockable AI chat panel with message bubbles
├── appsettings.json                      # API key configuration
└── BIMIntelligence.addin                 # Revit add-in manifest
```

### Key Design Decisions

- **ExternalEvent pattern** — WPF chat runs on its own thread; Revit API requires the main thread. `DataExtractionHandler` bridges them safely via `ExternalEvent.Raise()`.
- **Dynamic category scanning** — Instead of hardcoding categories, the plugin scans ALL elements and groups by category name. Works with architectural, structural, MEP, electrical, plumbing — any discipline.
- **Type-aware level resolution** — Each Revit element type stores its level in a different `BuiltInParameter` (Room uses `ROOM_LEVEL_ID`, Wall uses `WALL_BASE_CONSTRAINT`, Pipe uses `RBS_START_LEVEL_PARAM`, etc.). The plugin checks the correct parameter per type.
- **Auto-detected relationships** — Smart Extract scans all `FamilyInstance` elements and maps Host, Room, and SuperComponent relationships automatically. No manual configuration needed.
- **Retry with exponential backoff** — Up to 5 retries for API overloaded/rate-limited responses (2s, 4s, 8s, 16s, 30s delays).
- **Empty column filtering** — Smart Extract removes columns where all values are empty, keeping exports clean.

## Setup Instructions

### Prerequisites
- Autodesk Revit 2027
- .NET 10.0 SDK
- Anthropic API key ([console.anthropic.com](https://console.anthropic.com))

### Installation

```bash
# 1. Clone
git clone https://github.com/iamaravindh/revit.git
cd revit

# 2. Set your API key
# Edit BIMIntelligence/appsettings.json:
# { "AnthropicApiKey": "sk-ant-..." }

# 3. Build and publish
cd BIMIntelligence
dotnet publish -c Release -o "%APPDATA%\Autodesk\Revit\Addins\2027\BIMIntelligence"

# 4. Copy the add-in manifest
copy BIMIntelligence.addin "%APPDATA%\Autodesk\Revit\Addins\2027\"
```

Update the `<Assembly>` path in the copied `.addin` file to:
```xml
<Assembly>C:\Users\<YourUsername>\AppData\Roaming\Autodesk\Revit\Addins\2027\BIMIntelligence\BIMIntelligence.dll</Assembly>
```

Launch Revit 2027, click "Always Load" when prompted, and open any model.

## Sample File

Tested with **Snowdon Towers** sample files shipped with Revit 2027:
```
C:\Program Files\Autodesk\Revit 2027\Samples\Snowdon Towers Sample Architectural.rvt
C:\Program Files\Autodesk\Revit 2027\Samples\Snowdon Towers Sample Plumbing.rvt
```

The `rac_advanced_sample_project.rvt` is also included in the repository for reference.

## Assumptions

- Revit 2027 runs on .NET 10.0 (confirmed via `Revit.runtimeconfig.json`)
- Room areas converted from internal square feet to square meters
- Level elevations converted from internal feet to meters
- Only placed/bounded rooms (Area > 0) are included
- Doors and windows linked to rooms via `FromRoom`/`ToRoom` on `FamilyInstance`
- Host relationships detected via `FamilyInstance.Host` property
- Room containment detected via `FamilyInstance.Room` / `FamilyInstance.FromRoom`
- MEP connections detected via `FamilyInstance.SuperComponent`

## Tech Stack

- **C# / .NET 10.0** — matching Revit 2027's runtime
- **WPF** — DataGrid panels, dockable chat panel, dynamic column generation
- **Revit API** — FilteredElementCollector, Room, FamilyInstance, Level, View, ViewSheet, ExternalEvent
- **Anthropic Claude Sonnet 4** — LLM with tool calling for real-time model queries
- **Newtonsoft.Json** — JSON serialization for API and exports
