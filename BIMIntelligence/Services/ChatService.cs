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
    private const string Model = "claude-3-haiku-20240307";
    // Switch back when Sonnet 4 is available:
    // "claude-sonnet-4-20250514"

    private const string SystemPrompt = @"You are a BIM (Building Information Modeling) assistant integrated into Autodesk Revit.
You help users understand their building model by answering questions about ALL aspects — architectural, structural, electrical, mechanical, plumbing, and any other discipline.

You have TWO tools available:
1. extract_model_info — Use this FIRST for ANY question about the model. It dynamically scans EVERY element in the model and returns: project name, all levels with elevations, total element count, and a dictionary of ALL category names with their counts (e.g. Walls: 50, Lighting Fixtures: 120, Conduits: 85, etc.). This captures every Revit category automatically.
2. extract_room_data — Use this ONLY for detailed room-specific questions (room names, numbers, areas, doors/windows per room).

IMPORTANT: Always call extract_model_info first. Only use extract_room_data if the user asks about specific rooms.
Do NOT guess or make up data — always call a tool first.

After receiving the data, analyze it and respond in clear, natural language.
Include specific numbers when relevant. Only mention categories that have elements (count > 0).
Elevations are in meters (m). Room areas are in square meters (m²).";

    private static readonly object[] ToolDefinitions = new object[]
    {
        new
        {
            name = "extract_model_info",
            description = "Extracts a comprehensive summary of the Revit model by dynamically scanning ALL elements. Returns: project name, file path, total element count, all levels with elevations, and a dictionary of every category found with instance counts. This automatically captures all Revit categories (architectural, structural, MEP, electrical, plumbing, mechanical, etc.) without needing to know them in advance. Use this for ANY question about the building.",
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
            description = "Extracts detailed room data from the Revit model. Returns each room's name, number, level, area (in square meters), door count, and window count. Use this only when the user asks specifically about room details, room areas, or per-room door/window counts.",
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
        string toolResult)
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
                    input = new { }
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
                    ToolName = block["name"]?.ToString() ?? ""
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
}
