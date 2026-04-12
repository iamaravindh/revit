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

    private const string SystemPrompt = @"You are a BIM (Building Information Modeling) assistant integrated into Autodesk Revit.
You help users understand their building model by answering questions about rooms, doors, windows, levels, and areas.

When the user asks a question about the building model, use the extract_room_data tool to fetch live data from the currently open Revit model.
Do NOT guess or make up data — always call the tool first.

After receiving the data, analyze it and respond in clear, natural language.
Include specific numbers and room names when relevant.
If the user asks about area, note that values are in square meters (m²).";

    private static readonly object ToolDefinition = new
    {
        name = "extract_room_data",
        description = "Extracts room data from the currently open Revit model. Returns a list of all rooms with their name, number, level, area (in square meters), door count, and window count. Call this tool whenever the user asks about rooms, doors, windows, levels, or areas in the building.",
        input_schema = new
        {
            type = "object",
            properties = new { },
            required = Array.Empty<string>()
        }
    };

    public ChatService(string apiKey)
    {
        _apiKey = apiKey;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("x-api-key", _apiKey);
        _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
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
            tools = new[] { ToolDefinition },
            messages
        };

        var json = JsonConvert.SerializeObject(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(ApiUrl, content);
        var responseJson = await response.Content.ReadAsStringAsync();

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
                    name = "extract_room_data",
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
            tools = new[] { ToolDefinition },
            messages
        };

        var json = JsonConvert.SerializeObject(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(ApiUrl, content);
        var responseJson = await response.Content.ReadAsStringAsync();

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
