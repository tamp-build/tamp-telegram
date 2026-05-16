namespace Tamp.Telegram;

/// <summary>
/// Parse mode for a Telegram <c>sendMessage</c> payload. Plain text is
/// safest; MarkdownV2 + HTML both work but each has a strict escape
/// alphabet enforced server-side (a stray <c>_</c> or <c>*</c> in
/// MarkdownV2 returns HTTP 400 from the Bot API).
/// </summary>
public enum TelegramParseMode
{
    /// <summary>No formatting; the bot delivers the text verbatim.</summary>
    None,
    /// <summary>Telegram MarkdownV2. Caller is responsible for escaping per <see href="https://core.telegram.org/bots/api#markdownv2-style"/>.</summary>
    MarkdownV2,
    /// <summary>Telegram-flavored HTML. Caller escapes <c>&lt;</c>/<c>&gt;</c>/<c>&amp;</c>.</summary>
    Html,
}

/// <summary>
/// Outgoing message payload for the Telegram Bot API <c>sendMessage</c>
/// endpoint. Caller supplies the chat id + text; everything else is
/// optional.
/// </summary>
public sealed class TelegramMessage
{
    public TelegramMessage(string chatId, string text)
    {
        if (string.IsNullOrWhiteSpace(chatId)) throw new System.ArgumentException("chatId is required", nameof(chatId));
        if (text is null) throw new System.ArgumentNullException(nameof(text));
        ChatId = chatId;
        Text = text;
    }

    public string ChatId { get; }
    public string Text { get; set; }

    /// <summary>How the Telegram client should render <see cref="Text"/>. Default: <see cref="TelegramParseMode.None"/>.</summary>
    public TelegramParseMode ParseMode { get; set; } = TelegramParseMode.None;

    /// <summary>If <c>true</c>, Telegram suppresses the in-app notification (silent message). Default: <c>false</c>.</summary>
    public bool DisableNotification { get; set; }

    /// <summary>If <c>true</c>, link-preview previews in the message are suppressed. Default: <c>false</c>.</summary>
    public bool DisableLinkPreview { get; set; }
}

/// <summary>
/// Decoded result of a successful <c>sendMessage</c> — just the bits a
/// caller might want to log or correlate. The Bot API returns much more;
/// we only deserialize what's useful.
/// </summary>
public sealed class TelegramMessageResult
{
    public long MessageId { get; init; }
    public long ChatId { get; init; }
    public long DateUnix { get; init; }
}
