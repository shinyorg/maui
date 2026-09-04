using System.Diagnostics;
using Microsoft.Extensions.AI;
using Shiny.Maui.Controls.Chat;

using AIChatMessage = Microsoft.Extensions.AI.ChatMessage;
using ChatMessage = Shiny.Maui.Controls.Chat.ChatMessage;

namespace Sample.AI;

/// <summary>
/// Backs <c>ChatView</c> with the GitHub Copilot client and the app's generated navigation tools.
/// </summary>
/// <remarks>
/// ChatView is provider-driven - it owns the message list, paging and the typing indicator, and asks
/// this for data - so the conversation lives here rather than in the ViewModel. The provider is a
/// singleton and holds the history, because the control disposes its <see cref="IChatSession"/>
/// handle every time the page goes away.
///
/// <para>The client arrives via <see cref="Connect"/> once the ViewModel has finished the GitHub
/// device-code flow; until then a send is refused with <see cref="ChatSendRejectedException"/>, which
/// the control renders as a rejected bubble rather than a silent no-op.</para>
/// </remarks>
public class AiChatSessionProvider(AiMauiShellTools aiTools) : IChatSessionProvider
{
    public const string AiSessionId = "ai";
    public const string MeId = "me";
    public const string CopilotId = "copilot";

    readonly object sync = new();
    readonly List<ChatMessage> messages = [];
    readonly List<AIChatMessage> history = [];
    IChatClient? chatClient;
    int idCounter;

    internal object Sync => this.sync;
    internal List<ChatMessage> Messages => this.messages;
    internal IChatClient? Client => this.chatClient;
    internal AiMauiShellTools Tools => aiTools;
    internal AIChatMessage SystemPrompt => this.history[0];

    public bool IsConnected => this.chatClient != null;


    /// <summary>
    /// Hands the provider an authenticated client and seeds the conversation. Called by the
    /// ViewModel once the device-code flow completes.
    /// </summary>
    public void Connect(IChatClient client)
    {
        lock (this.sync)
        {
            this.chatClient = client;
            this.history.Clear();
            this.messages.Clear();

            Debug.WriteLine($"[AI] AiRoutePrompt:\n{aiTools.Prompt}");
            this.history.Add(new AIChatMessage(ChatRole.System,
                $"""
                You are a helpful assistant integrated in a .NET MAUI app. You can navigate the user to pages and pre-fill forms using the NavigateToRoute tool.

                {aiTools.Prompt}
                When the user describes a problem, request, or intent that matches a route, call NavigateToRoute immediately with the appropriate route and parameters inferred from what the user said. Do not ask the user to confirm parameters unless something is genuinely ambiguous.

                When the user first greets you or asks what you can do, briefly describe your capabilities based on the available routes above.
                """));

            // Seeded rather than pushed through MessageReceived: the control has not attached yet -
            // it is still hidden behind the login UI - and this is the first page it will read.
            var capabilities = String.Join(
                "\n",
                aiTools.GetAiToolApplicableGeneratedRoutes().Select(x => $"- {x.Description}")
            );
            this.messages.Add(this.NewMessage(
                CopilotId,
                $"Hi! I'm your AI assistant. Here's what I can help with:\n{capabilities}\n\nJust describe what you need and I'll take care of the rest!"
            ));
        }
    }


    public void Disconnect()
    {
        lock (this.sync)
        {
            this.chatClient = null;
            this.history.Clear();
            this.messages.Clear();
        }
    }


    internal ChatMessage NewMessage(string senderId, string? body) => new(
        MessageId: $"m{Interlocked.Increment(ref this.idCounter)}",
        ClientMessageId: null,
        SenderId: senderId,
        Body: body,
        ImageUrl: null,
        Status: MessageStatus.Sent,
        StatusReason: null,
        Timestamp: DateTimeOffset.Now,
        EditedTimestamp: null,
        Reactions: [],
        ReadReceipts: []
    );


    internal ChatSessionInfo BuildInfo() => new(
        SessionId: AiSessionId,
        SessionName: "AI Assistant",
        Users:
        [
            new ChatSessionUserInfo(MeId, "You", null, null, DateTimeOffset.Now),
            new ChatSessionUserInfo(CopilotId, "AI", null, Color.FromArgb("#E8E8E8"), DateTimeOffset.Now)
        ],
        PermittedEmojis: [],                                // an assistant has nothing to react to
        BodyPermissions: MessageBodyPermissions.None,       // plain text in, markdown out
        Permissions: ChatSessionPermissions.CanSendMessages,
        CreatedAt: DateTimeOffset.Now,
        LastReadDate: DateTimeOffset.Now,
        UnreadMessageCount: 0
    );


    public Task<IChatSession> CreateSessionAsync(string[] userIds, CancellationToken cancellationToken = default)
        => this.GetSessionAsync(AiSessionId, cancellationToken);


    public Task<IChatSession> GetSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        if (sessionId != AiSessionId)
            throw new ChatSessionException($"Chat session '{sessionId}' was not found.");

        return Task.FromResult<IChatSession>(new AiChatSession(this));
    }
}


/// <summary>
/// One attachment of the control to the AI conversation. State lives in the provider; this is the
/// handle the control talks to and the thing that raises events at it.
/// </summary>
sealed class AiChatSession(AiChatSessionProvider provider) : IChatSession, IAsyncDisposable
{
    readonly CancellationTokenSource cancel = new();

    public ChatSessionInfo Info => provider.BuildInfo();
    public string CurrentUserId => AiChatSessionProvider.MeId;

    public event EventHandler<ChatMessage>? MessageReceived;
    public event EventHandler<MessageChanged>? MessageUpdated;
    public event EventHandler<String>? MessageDeleted;
    public event EventHandler<UserTypingEvent>? UserTyping;
    public event EventHandler<ChatSessionUserInfo>? UserJoined;
    public event EventHandler<ChatSessionUserInfo>? UserLeft;
    public event EventHandler<ChatSessionInfo>? SessionUpdated;
    public event EventHandler<ChatConnectionState>? ConnectionStateChanged;


    public Task<MessagePage> GetMessagesAsync(
        string? cursorMessageId,
        MessagePageDirection direction,
        int count,
        CancellationToken cancellationToken = default
    )
    {
        lock (provider.Sync)
        {
            var all = provider.Messages;

            if (direction == MessagePageDirection.Older)
            {
                var end = all.Count;
                if (cursorMessageId != null)
                {
                    var index = all.FindIndex(x => x.MessageId == cursorMessageId);
                    if (index >= 0)
                        end = index;                    // strictly older than the cursor
                }

                var start = Math.Max(0, end - count);
                return Task.FromResult(new MessagePage(all.GetRange(start, end - start), start > 0));
            }
            else
            {
                var start = 0;
                if (cursorMessageId != null)
                {
                    var index = all.FindIndex(x => x.MessageId == cursorMessageId);
                    if (index >= 0)
                        start = index + 1;              // strictly newer than the cursor
                }

                var take = Math.Min(count, all.Count - start);
                if (take <= 0)
                    return Task.FromResult(new MessagePage([], false));

                return Task.FromResult(new MessagePage(all.GetRange(start, take), start + take < all.Count));
            }
        }
    }


    public Task<ChatMessage> SendMessageAsync(OutgoingMessage message, CancellationToken cancellationToken = default)
    {
        if (!provider.IsConnected)
            throw new ChatSendRejectedException("Sign in with GitHub before chatting.", SendRejectionKind.NotPermitted);

        var body = message.Body ?? String.Empty;
        ChatMessage stored;
        lock (provider.Sync)
        {
            stored = provider.NewMessage(this.CurrentUserId, body) with
            {
                ClientMessageId = String.IsNullOrEmpty(message.ClientMessageId) ? null : message.ClientMessageId
            };
            provider.Messages.Add(stored);
        }

        _ = this.RespondAsync(body);
        return Task.FromResult(stored);
    }


    public Task<ChatMessage> ResendMessageAsync(string clientMessageId, CancellationToken cancellationToken = default)
    {
        ChatMessage resent;
        lock (provider.Sync)
        {
            var index = provider.Messages.FindIndex(x => x.ClientMessageId == clientMessageId);
            if (index < 0)
                throw new ChatSessionException("Message to resend was not found.");

            resent = provider.Messages[index] with { Status = MessageStatus.Sent, StatusReason = null };
            provider.Messages[index] = resent;
        }

        // Re-asking is the whole point of a retry here - the message itself never failed to send,
        // the answer did.
        _ = this.RespondAsync(resent.Body ?? String.Empty);
        return Task.FromResult(resent);
    }


    /// <summary>
    /// Asks the model and pushes its answer in as a received message. The typing indicator is a pair
    /// of <see cref="UserTyping"/> events around the call - the control owns the animation.
    /// </summary>
    async Task RespondAsync(string prompt)
    {
        var client = provider.Client;
        if (client == null)
            return;

        this.UserTyping?.Invoke(this, new UserTypingEvent(AiChatSessionProvider.CopilotId, true, DateTimeOffset.Now));
        try
        {
            var options = new ChatOptions { Tools = [.. provider.Tools.Tools] };

            Debug.WriteLine("[AI] Registered tools:");
            foreach (var tool in provider.Tools.Tools)
                Debug.WriteLine($"  - {tool}");

            // System prompt + the current turn, deliberately not the whole history.
            var request = new List<AIChatMessage>
            {
                provider.SystemPrompt,
                new(ChatRole.User, prompt)
            };

            var response = await client
                .GetResponseAsync(request, options, this.cancel.Token)
                .ConfigureAwait(false);

            LogResponse(response);
            this.Publish(AiChatSessionProvider.CopilotId, response.Text ?? "(no response)");
        }
        catch (OperationCanceledException)
        {
            // The page went away mid-answer - nothing to report.
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AI] ERROR: {ex}");
            this.Publish(AiChatSessionProvider.CopilotId, $"Error: {ex.Message}");
        }
        finally
        {
            this.UserTyping?.Invoke(this, new UserTypingEvent(AiChatSessionProvider.CopilotId, false, DateTimeOffset.Now));
        }
    }


    void Publish(string senderId, string body)
    {
        ChatMessage message;
        lock (provider.Sync)
        {
            message = provider.NewMessage(senderId, body);
            provider.Messages.Add(message);
        }
        this.MessageReceived?.Invoke(this, message);
    }


    static void LogResponse(ChatResponse response)
    {
        Debug.WriteLine($"[AI] Response messages: {response.Messages.Count}");
        foreach (var message in response.Messages)
        {
            Debug.WriteLine($"[AI]   Role={message.Role}, Contents={message.Contents.Count}");
            foreach (var content in message.Contents)
            {
                switch (content)
                {
                    case TextContent text:
                        Debug.WriteLine($"[AI]     TextContent: {text.Text?[..Math.Min(200, text.Text?.Length ?? 0)]}");
                        break;

                    case FunctionCallContent call:
                        Debug.WriteLine($"[AI]     FunctionCall: {call.Name}({System.Text.Json.JsonSerializer.Serialize(call.Arguments)})");
                        break;

                    case FunctionResultContent result:
                        Debug.WriteLine($"[AI]     FunctionResult: CallId={result.CallId}, Result={result.Result}");
                        break;

                    default:
                        Debug.WriteLine($"[AI]     {content.GetType().Name}: {content}");
                        break;
                }
            }
        }
    }


    // Everything below is gated off by ChatSessionPermissions.CanSendMessages - the control never
    // surfaces an affordance for any of it against an assistant.
    public Task EditMessageAsync(string messageId, string body, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task DeleteMessageAsync(string messageId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task ReactToMessageAsync(string messageId, string emoji, bool add, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task MarkReadAsync(string[] messageIds, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task ToggleTypingAsync(bool isTyping, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task InviteUserAsync(string userId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task LeaveAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task RenameAsync(string sessionName, CancellationToken cancellationToken = default)
        => Task.CompletedTask;


    public ValueTask DisposeAsync()
    {
        // Detaching cancels an answer that is still streaming - the old ViewModel did this from
        // OnDisappearing, and the control's own lifecycle is the more reliable hook for it.
        this.cancel.Cancel();
        this.cancel.Dispose();
        return ValueTask.CompletedTask;
    }
}
