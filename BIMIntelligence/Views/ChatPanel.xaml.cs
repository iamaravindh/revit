using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Autodesk.Revit.UI;
using BIMIntelligence.Models;
using BIMIntelligence.Services;
using Newtonsoft.Json;

namespace BIMIntelligence.Views;

public partial class ChatPanel : Page, IDockablePaneProvider
{
    private ChatService? _chatService;
    private readonly List<ChatMessage> _conversationHistory = new();
    private bool _isProcessing;

    public ChatPanel()
    {
        InitializeComponent();
        InitializeChatService();
        AddBotMessage("Hello! I'm your BIM assistant. Ask me anything about the building model — rooms, doors, windows, levels, or areas.");
    }

    public void SetupDockablePane(DockablePaneProviderData data)
    {
        data.FrameworkElement = this;
        data.InitialState = new DockablePaneState
        {
            DockPosition = DockPosition.Right
        };
    }

    private void InitializeChatService()
    {
        try
        {
            var configPath = Path.Combine(
                Path.GetDirectoryName(typeof(ChatPanel).Assembly.Location) ?? "",
                "appsettings.json");

            if (File.Exists(configPath))
            {
                var config = JsonConvert.DeserializeObject<Dictionary<string, string>>(
                    File.ReadAllText(configPath));

                if (config != null && config.TryGetValue("AnthropicApiKey", out var apiKey)
                    && !string.IsNullOrWhiteSpace(apiKey) && apiKey != "YOUR_API_KEY_HERE")
                {
                    _chatService = new ChatService(apiKey);
                    return;
                }
            }

            AddBotMessage("Please set your Anthropic API key in appsettings.json (located next to the plugin DLL) and restart Revit.");
        }
        catch (Exception ex)
        {
            AddBotMessage($"Failed to load config: {ex.Message}");
        }
    }

    private void OnSendClick(object sender, RoutedEventArgs e)
    {
        SendMessage();
    }

    private void OnInputKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            SendMessage();
            e.Handled = true;
        }
    }

    private async void SendMessage()
    {
        var question = InputBox.Text.Trim();
        if (string.IsNullOrEmpty(question) || _isProcessing) return;

        if (_chatService == null)
        {
            AddBotMessage("Chat service is not configured. Please set your API key in appsettings.json.");
            return;
        }

        _isProcessing = true;
        SendButton.IsEnabled = false;
        InputBox.Text = "";

        AddUserMessage(question);
        AddBotMessage("Thinking...");

        try
        {
            // Step 1: Send question to Claude
            var response = await _chatService.SendMessageAsync(_conversationHistory, question);

            if (response.Type == ChatResponseType.ToolUse)
            {
                // Step 2: Claude wants data — run extraction on Revit's thread
                UpdateLastBotMessage($"Extracting data from model...");

                var taskSource = new TaskCompletionSource<string>();

                // Set which tool to run
                App.DataExtractionHandler.ToolName = response.ToolName;

                // Pass category_name if the tool requires it
                if (response.ToolName == "extract_category_data" && response.ToolInput != null)
                {
                    App.DataExtractionHandler.CategoryFilter =
                        response.ToolInput["category_name"]?.ToString() ?? "";
                }

                App.DataExtractionHandler.OnDataExtracted = (jsonData) =>
                {
                    taskSource.TrySetResult(jsonData);
                };

                App.DataExtractionHandler.OnError = (error) =>
                {
                    taskSource.TrySetException(new Exception(error));
                };

                // Raise the external event — Revit will call Execute() on its thread
                App.DataExtractionEvent.Raise();

                // Wait for the extraction to complete
                var toolResult = await taskSource.Task;

                // Step 3: Send tool result back to Claude
                var finalResponse = await _chatService.SendToolResultAsync(
                    _conversationHistory, question, response.ToolUseId, response.ToolName, toolResult, response.ToolInput);

                UpdateLastBotMessage(finalResponse.Text);

                // Update conversation history
                _conversationHistory.Add(new ChatMessage { Role = "user", Content = question });
                _conversationHistory.Add(new ChatMessage { Role = "assistant", Content = finalResponse.Text });
            }
            else
            {
                // Direct text response (no tool call needed)
                UpdateLastBotMessage(response.Text);
                _conversationHistory.Add(new ChatMessage { Role = "user", Content = question });
                _conversationHistory.Add(new ChatMessage { Role = "assistant", Content = response.Text });
            }
        }
        catch (Exception ex)
        {
            UpdateLastBotMessage($"Error: {ex.Message}");
        }
        finally
        {
            _isProcessing = false;
            SendButton.IsEnabled = true;
        }
    }

    private void AddUserMessage(string text)
    {
        var bubble = CreateBubble(text, isUser: true);
        MessagesPanel.Children.Add(bubble);
        ChatScroller.ScrollToEnd();
    }

    private void AddBotMessage(string text)
    {
        var bubble = CreateBubble(text, isUser: false);
        MessagesPanel.Children.Add(bubble);
        ChatScroller.ScrollToEnd();
    }

    private void UpdateLastBotMessage(string text)
    {
        // Find the last bot message and update its text
        for (int i = MessagesPanel.Children.Count - 1; i >= 0; i--)
        {
            if (MessagesPanel.Children[i] is Border border && border.Child is TextBlock tb)
            {
                if (border.Tag?.ToString() == "bot")
                {
                    tb.Text = text;
                    ChatScroller.ScrollToEnd();
                    return;
                }
            }
        }
    }

    private Border CreateBubble(string text, bool isUser)
    {
        return new Border
        {
            Tag = isUser ? "user" : "bot",
            Background = isUser
                ? new SolidColorBrush(Color.FromRgb(37, 99, 235))
                : new SolidColorBrush(Color.FromRgb(243, 244, 246)),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(12, 8, 12, 8),
            Margin = isUser
                ? new Thickness(60, 4, 4, 4)
                : new Thickness(4, 4, 60, 4),
            HorizontalAlignment = isUser
                ? HorizontalAlignment.Right
                : HorizontalAlignment.Left,
            MaxWidth = 400,
            Child = new TextBlock
            {
                Text = text,
                Foreground = isUser ? Brushes.White : new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                TextWrapping = TextWrapping.Wrap,
                FontSize = 13
            }
        };
    }
}
