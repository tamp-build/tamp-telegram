using System.Text.Json.Serialization;
using Tamp.Http;

namespace Tamp.Telegram;

/// <summary>
/// Thin client for the Telegram Bot API <c>sendMessage</c> endpoint
/// (the only verb the Tamp.Telegram surface needs today). Bot token is
/// embedded in the URL path per Telegram's own auth model
/// (<c>https://api.telegram.org/bot&lt;TOKEN&gt;/&lt;METHOD&gt;</c>),
/// so the underlying <see cref="ApiCredential"/> stays
/// <see cref="ApiCredential.None"/>.
/// </summary>
/// <remarks>
/// The bot token is a sensitive value — wrap it in a <see cref="Secret"/>
/// at the call site. The client constructs the URL inline; the token
/// never lands in framework logs, but adopters embedding the client in
/// a wider Build.cs should still treat the token as redaction-eligible.
/// </remarks>
public sealed class TelegramClient : TampApiClient
{
    /// <summary>
    /// Construct a Telegram client.
    /// </summary>
    /// <param name="botToken">HTTP API token from BotFather. Wrap in <see cref="Secret"/> to keep it out of process tables / logs.</param>
    /// <param name="disableConnectionVerification">Disable TLS validation. For environments behind a TLS-intercepting proxy (Zscaler).</param>
    /// <param name="http">Optional injected <see cref="HttpClient"/>. Used by tests.</param>
    public TelegramClient(Secret botToken, bool disableConnectionVerification = false, HttpClient? http = null)
        : base(
            baseUri: TelegramClientSettings.BuildBaseUri(botToken ?? throw new System.ArgumentNullException(nameof(botToken))),
            credential: ApiCredential.None,
            disableConnectionVerification: disableConnectionVerification,
            http: http,
            userAgent: "Tamp.Telegram/0.1.0")
    {
    }

    /// <summary>
    /// POST <c>sendMessage</c>. Returns the Telegram message id + chat id
    /// on success. 4xx / 5xx responses surface as <see cref="ApiException"/>
    /// with the API's error body captured for diagnostics.
    /// </summary>
    public async System.Threading.Tasks.Task<TelegramMessageResult> SendMessageAsync(
        TelegramMessage message,
        System.Threading.CancellationToken ct = default)
    {
        if (message is null) throw new System.ArgumentNullException(nameof(message));

        var body = new SendMessageRequest
        {
            ChatId = message.ChatId,
            Text = message.Text,
            ParseMode = message.ParseMode switch
            {
                TelegramParseMode.MarkdownV2 => "MarkdownV2",
                TelegramParseMode.Html => "HTML",
                _ => null,
            },
            DisableNotification = message.DisableNotification ? true : null,
            LinkPreviewOptions = message.DisableLinkPreview ? new LinkPreviewOptions { IsDisabled = true } : null,
        };

        var resp = await PostJsonAsync<SendMessageResponse>("sendMessage", body, ct).ConfigureAwait(false);
        if (!resp.Ok || resp.Result is null)
        {
            throw new ApiException(
                System.Net.HttpStatusCode.BadRequest,
                "sendMessage",
                "POST",
                resp.Description,
                $"Telegram sendMessage returned ok=false: {resp.Description}");
        }
        return new TelegramMessageResult
        {
            MessageId = resp.Result.MessageId,
            ChatId = resp.Result.Chat.Id,
            DateUnix = resp.Result.Date,
        };
    }

    // ── Wire types — match Telegram Bot API exactly so the deserializer maps cleanly. ──

    private sealed class SendMessageRequest
    {
        [JsonPropertyName("chat_id")]               public string ChatId { get; init; } = "";
        [JsonPropertyName("text")]                  public string Text { get; init; } = "";
        [JsonPropertyName("parse_mode")]            public string? ParseMode { get; init; }
        [JsonPropertyName("disable_notification")]  public bool? DisableNotification { get; init; }
        [JsonPropertyName("link_preview_options")]  public LinkPreviewOptions? LinkPreviewOptions { get; init; }
    }

    private sealed class LinkPreviewOptions
    {
        [JsonPropertyName("is_disabled")] public bool IsDisabled { get; init; }
    }

    private sealed class SendMessageResponse
    {
        [JsonPropertyName("ok")]          public bool Ok { get; init; }
        [JsonPropertyName("description")] public string? Description { get; init; }
        [JsonPropertyName("result")]      public ResultBody? Result { get; init; }
    }

    private sealed class ResultBody
    {
        [JsonPropertyName("message_id")] public long MessageId { get; init; }
        [JsonPropertyName("date")]       public long Date { get; init; }
        [JsonPropertyName("chat")]       public ChatBody Chat { get; init; } = new();
    }

    private sealed class ChatBody
    {
        [JsonPropertyName("id")] public long Id { get; init; }
    }
}
