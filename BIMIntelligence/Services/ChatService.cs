using System.Net.Http;
using System.Text;
using BIMIntelligence.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BIMIntelligence.Services;

public class ChatService
{
    private readonly string _apiKey;
    private readonly HttpClient _httpClient;
    private const string ApiUrl = "https://api.anthropic.com/v1/messages";
    private const string Model = "claude-sonnet-4-20250514";

    private const string SystemPrompt = @"You are a BIM assistant running INSIDE Autodesk Revit as a plugin. You ARE directly connected to the currently open Revit model and can see what the user is currently viewing.

You have FIVE tools:
1. extract_current_view — What the user is currently viewing. Returns: view name/type/level, visible element categories with counts, all sheets, all views.
2. extract_model_info — Overall model summary. Returns: project name, levels, total elements, ALL category counts.
3. extract_room_data — Detailed room data (names, numbers, areas, doors/windows per room).
4. extract_category_data — Deep dive into a specific category grouped by level with family/type/size. Requires category_name input.
5. extract_relationships — Shows how elements relate to each other: which categories host/contain which (e.g. Walls host 50 Doors, Room 'Lobby' contains 12 Furniture). Shows host, room containment, and component connections with top examples.

Strategy:
- 'what is in the model' → extract_model_info
- 'what am I looking at' → extract_current_view
- 'how many pipes per level' → extract_category_data
- room-specific questions → extract_room_data
- 'what doors are in walls', 'what fixtures in rooms', 'show relationships', 'what is hosted by walls' → extract_relationships
- If unsure of exact category name, call extract_model_info first.

Rules:
- Always call a tool. Never guess.
- You ARE connected to the live Revit model. Never say otherwise.
- Be concise and confident.
- When listing rooms or elements, ALWAYS show specific names, numbers, and values — never give generic summaries like 'the query returned rooms'. List them.
- Format lists cleanly with bullet points or tables.";

    private static readonly object[] ToolDefinitions = new object[]
    {
        new
        {
            name = "extract_current_view",
            description = "Extracts information about what the user is currently viewing in Revit. Returns: active view name, view type (FloorPlan, Section, 3D, Sheet, etc.), associated level, scale, detail level, count and categories of all elements visible in the current view, list of ALL sheets in the model, and list of ALL views. Use this when the user asks about their current view, visible elements, sheets, floor plans, or what they're looking at.",
            input_schema = new
            {
                type = "object",
                properties = new { },
                required = Array.Empty<string>()
            }
        },
        new
        {
            name = "extract_model_info",
            description = "Extracts a comprehensive summary of the entire Revit model by dynamically scanning ALL elements. Returns: project name, file path, total element count, all levels with elevations, and a dictionary of every category found with instance counts. Use this for general questions about the building model.",
            input_schema = new
            {
                type = "object",
                properties = new { },
                required = Array.Empty<string>()
            }
        },
        new
        {
            name = "extract_room_data",
            description = "Extracts detailed room data. Returns each room's name, number, level, area (m²), door count, and window count. Use only for room-specific questions.",
            input_schema = new
            {
                type = "object",
                properties = new { },
                required = Array.Empty<string>()
            }
        },
        new
        {
            name = "extract_category_data",
            description = "Extracts detailed data for a specific element category, grouped by level. Returns total count, and for each level: element count and breakdown by family/type/size. Use this when the user asks about specific elements like pipes, ducts, walls, doors, columns, beams, fixtures etc. — especially questions about which level they are on, how many per level, what types/sizes are used. You MUST provide the category_name input.",
            input_schema = new
            {
                type = "object",
                properties = new
                {
                    category_name = new
                    {
                        type = "string",
                        description = "The exact Revit category name (e.g. 'Pipes', 'Walls', 'Doors', 'Structural Columns', 'Ducts', 'Plumbing Fixtures', 'Lighting Fixtures'). Use extract_model_info first if you don't know the exact category name."
                    }
                },
                required = new[] { "category_name" }
            }
        },
        new
        {
            name = "extract_relationships",
            description = "Extracts the full relationship map showing how elements are connected in the model. Returns three types of relationships: (1) Host relationships — which categories host which (e.g. Walls host 50 Doors, Ceilings host 30 Lighting Fixtures), with top examples showing specific host elements and their child counts. (2) Room containment — which categories are inside rooms (e.g. Room 'Lobby' contains 12 Furniture), with examples per room. (3) Component connections — parent-child MEP connections (e.g. Pipes connect to Pipe Fittings). Use this when the user asks about element relationships, what is hosted by what, what is inside rooms, or how elements connect.",
            input_schema = new
            {
                type = "object",
                properties = new { },
                required = Array.Empty<string>()
            }
        }
    };

    private const int MaxRetries = 5;
    private static readonly int[] RetryDelaysMs = { 2000, 4000, 8000, 16000, 30000 };

    public ChatService(string apiKey)
    {
        _apiKey = apiKey;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("x-api-key", _apiKey);
        _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
    }

    /// <summary>
    /// Posts JSON to the API with automatic retry on overloaded (529) or rate limit (429) errors.
    /// </summary>
    private async Task<(HttpResponseMessage response, string body)> PostWithRetryAsync(string json)
    {
        for (int attempt = 0; attempt <= MaxRetries; attempt++)
        {
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(ApiUrl, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            // Retry on 529 (overloaded) or 429 (rate limited)
            if (((int)response.StatusCode == 529 || (int)response.StatusCode == 429) && attempt < MaxRetries)
            {
                await Task.Delay(RetryDelaysMs[attempt]);
                continue;
            }

            return (response, responseBody);
        }

        // Should not reach here, but just in case
        return (new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable),
                "{\"error\":{\"message\":\"Max retries exceeded. API is still overloaded.\"}}");
    }

    /// <summary>
    /// Sends a user question to Claude. If Claude requests tool use, returns the tool request.
    /// Otherwise returns the text response directly.
    /// </summary>
    public async Task<ChatResponse> SendMessageAsync(List<ChatMessage> conversationHistory, string userMessage)
    {
        // Build messages array for the API
        var messages = new List<object>();
        foreach (var msg in conversationHistory)
        {
            messages.Add(new { role = msg.Role, content = msg.Content });
        }
        messages.Add(new { role = "user", content = userMessage });

        var requestBody = new
        {
            model = Model,
            max_tokens = 1024,
            system = SystemPrompt,
            tools = ToolDefinitions,
            messages
        };

        var json = JsonConvert.SerializeObject(requestBody);
        var (response, responseJson) = await PostWithRetryAsync(json);

        if (!response.IsSuccessStatusCode)
        {
            return new ChatResponse
            {
                Type = ChatResponseType.Text,
                Text = $"API Error: {response.StatusCode}\n{responseJson}"
            };
        }

        var result = JObject.Parse(responseJson);
        return ParseResponse(result);
    }

    /// <summary>
    /// Sends the tool result back to Claude and gets the final natural language response.
    /// </summary>
    public async Task<ChatResponse> SendToolResultAsync(
        List<ChatMessage> conversationHistory,
        string userMessage,
        string toolUseId,
        string toolName,
        string toolResult,
        JObject? toolInput = null)
    {
        var messages = new List<object>();
        foreach (var msg in conversationHistory)
        {
            messages.Add(new { role = msg.Role, content = msg.Content });
        }

        // Add user message
        messages.Add(new { role = "user", content = userMessage });

        // Add assistant's tool_use response
        messages.Add(new
        {
            role = "assistant",
            content = new[]
            {
                new
                {
                    type = "tool_use",
                    id = toolUseId,
                    name = toolName,
                    input = toolInput ?? new JObject()
                }
            }
        });

        // Add tool result
        messages.Add(new
        {
            role = "user",
            content = new[]
            {
                new
                {
                    type = "tool_result",
                    tool_use_id = toolUseId,
                    content = toolResult
                }
            }
        });

        var requestBody = new
        {
            model = Model,
            max_tokens = 1024,
            system = SystemPrompt,
            tools = ToolDefinitions,
            messages
        };

        var json = JsonConvert.SerializeObject(requestBody);
        var (response, responseJson) = await PostWithRetryAsync(json);

        if (!response.IsSuccessStatusCode)
        {
            return new ChatResponse
            {
                Type = ChatResponseType.Text,
                Text = $"API Error: {response.StatusCode}\n{responseJson}"
            };
        }

        var result = JObject.Parse(responseJson);
        return ParseResponse(result);
    }

    private ChatResponse ParseResponse(JObject result)
    {
        var contentArray = result["content"] as JArray;
        if (contentArray == null || contentArray.Count == 0)
        {
            return new ChatResponse { Type = ChatResponseType.Text, Text = "No response from AI." };
        }

        // Check if the response contains a tool_use block
        foreach (var block in contentArray)
        {
            var blockType = block["type"]?.ToString();

            if (blockType == "tool_use")
            {
                return new ChatResponse
                {
                    Type = ChatResponseType.ToolUse,
                    ToolUseId = block["id"]?.ToString() ?? "",
                    ToolName = block["name"]?.ToString() ?? "",
                    ToolInput = block["input"] as JObject
                };
            }
        }

        // Otherwise, extract text
        var textParts = contentArray
            .Where(b => b["type"]?.ToString() == "text")
            .Select(b => b["text"]?.ToString() ?? "");

        return new ChatResponse
        {
            Type = ChatResponseType.Text,
            Text = string.Join("\n", textParts)
        };
    }
}

public enum ChatResponseType
{
    Text,
    ToolUse
}

public class ChatResponse
{
    public ChatResponseType Type { get; set; }
    public string Text { get; set; } = string.Empty;
    public string ToolUseId { get; set; } = string.Empty;
    public string ToolName { get; set; } = string.Empty;
    public JObject? ToolInput { get; set; }
}
