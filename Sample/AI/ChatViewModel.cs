using System.ClientModel;
using System.ClientModel.Primitives;
using Microsoft.Extensions.AI;
using OpenAI;
using Shiny;
using Shiny.Maui.Controls.Chat;

namespace Sample.AI;

/// <summary>
/// The GitHub device-code flow and nothing else - <c>ChatView</c> is provider-driven, so the
/// conversation itself lives in <see cref="AiChatSessionProvider"/> rather than in a message
/// collection here.
/// </summary>
[ShellMap<ChatPage>]
public partial class ChatViewModel(
    AiChatSessionProvider provider,
    GitHubCopilotAuthService authService
) : ObservableObject, IPageLifecycleAware
{
    CancellationTokenSource? cts;

    public IChatSessionProvider Provider => provider;

    /// <summary>
    /// Null until connected. Setting it is what makes the control attach and load, and clearing it
    /// on logout is what makes it drop the previous conversation.
    /// </summary>
    [ObservableProperty] string? sessionId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    bool isBusy;

    [ObservableProperty] string authStatus = "Not authenticated";
    [ObservableProperty] string? userCode;
    [ObservableProperty] bool isAuthenticated;

    public bool IsNotBusy => !this.IsBusy;


    public async void OnAppearing()
    {
        if (this.IsAuthenticated || this.IsBusy)
            return;

        this.IsBusy = true;
        this.AuthStatus = "Restoring session...";

        try
        {
            if (await authService.TryRestoreSessionAsync())
            {
                this.Connect();
                this.AuthStatus = "Ready to chat";
            }
            else
            {
                this.AuthStatus = "Not authenticated";
            }
        }
        catch
        {
            this.AuthStatus = "Not authenticated";
        }
        finally
        {
            this.IsBusy = false;
        }
    }


    public void OnDisappearing() => this.cts?.Cancel();


    [RelayCommand]
    async Task Login()
    {
        if (this.IsAuthenticated)
            return;

        try
        {
            this.IsBusy = true;
            this.AuthStatus = "Requesting device code...";
            this.cts = new CancellationTokenSource();

            var deviceCode = await authService.RequestDeviceCodeAsync(this.cts.Token);
            this.UserCode = deviceCode.UserCode;
            this.AuthStatus = $"Enter code {deviceCode.UserCode} at {deviceCode.VerificationUri}";

            await Browser.Default.OpenAsync(deviceCode.VerificationUri, BrowserLaunchMode.External);

            this.AuthStatus = "Waiting for authorization...";
            var success = await authService.PollForAccessTokenAsync(
                deviceCode.DeviceCode,
                deviceCode.Interval,
                this.cts.Token
            );

            if (success)
            {
                this.AuthStatus = "Authenticated! Setting up AI client...";
                this.Connect();
                this.UserCode = null;
                this.AuthStatus = "Ready to chat";
            }
            else
            {
                this.AuthStatus = "Authentication failed or expired. Try again.";
            }
        }
        catch (OperationCanceledException)
        {
            this.AuthStatus = "Authentication cancelled.";
        }
        catch (Exception ex)
        {
            this.AuthStatus = $"Error: {ex.Message}";
        }
        finally
        {
            this.IsBusy = false;
        }
    }


    void Connect()
    {
        var transport = new CopilotTokenHandler(authService, new HttpClientHandler());

        var client = new OpenAIClient(
            new ApiKeyCredential("copilot-placeholder"),
            new OpenAIClientOptions
            {
                Transport = new HttpClientPipelineTransport(new HttpClient(transport)),
                Endpoint = new Uri("https://api.githubcopilot.com")
            }
        );

        var chatClient = client
            .GetChatClient("gpt-4.1")
            .AsIChatClient()
            .AsBuilder()
            .UseFunctionInvocation()
            .Build();

        provider.Connect(chatClient);

        // Order matters: the control resolves the session as soon as it has a SessionId, and the
        // provider has to be holding the seeded conversation by then.
        this.SessionId = AiChatSessionProvider.AiSessionId;
        this.IsAuthenticated = true;
    }


    [RelayCommand]
    void Logout()
    {
        authService.Logout();
        provider.Disconnect();

        // Clearing the id detaches the control, so a later login starts from an empty conversation
        // instead of the previous session's messages.
        this.SessionId = null;
        this.IsAuthenticated = false;
        this.AuthStatus = "Not authenticated";
    }
}
