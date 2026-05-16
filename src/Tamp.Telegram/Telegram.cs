namespace Tamp.Telegram;

/// <summary>
/// Top-level facade for sending Telegram messages from a Tamp build.
/// Mirrors the satellite-shape convention used by Tamp.Yarn, Tamp.Docker,
/// etc., though this is an HTTP-API satellite (not a CLI wrapper), so the
/// surface is much smaller — one verb.
///
/// For build-failure notifications wire <see cref="TelegramBuildReporter"/>
/// via the framework's <c>[BuildReporter]</c> attribute. The static
/// surface here is for adopters who want to send their own messages
/// (deploy succeeded, release published, custom signal) from a target body.
/// </summary>
public static class Telegram
{
    /// <summary>
    /// Send a Telegram message synchronously. Returns the message id +
    /// chat id; throws <see cref="Tamp.Http.ApiException"/> on 4xx/5xx.
    ///
    /// <example>
    /// <code>
    /// Target NotifyDeploy =&gt; _ =&gt; _.Executes(() =&gt;
    ///     Telegram.SendMessage(BotToken, ChatId, $"✅ deploy of {Version} complete"));
    /// </code>
    /// </example>
    /// </summary>
    public static TelegramMessageResult SendMessage(Secret botToken, string chatId, string text, TelegramParseMode parseMode = TelegramParseMode.None)
    {
        using var client = new TelegramClient(botToken);
        return client.SendMessageAsync(new TelegramMessage(chatId, text) { ParseMode = parseMode })
            .GetAwaiter().GetResult();
    }

    /// <summary>
    /// Send a Telegram message with a fully-configured <see cref="TelegramMessage"/>.
    /// Use this overload when you need to override DisableNotification, link-preview
    /// behavior, etc.
    /// </summary>
    public static TelegramMessageResult SendMessage(Secret botToken, TelegramMessage message)
    {
        using var client = new TelegramClient(botToken);
        return client.SendMessageAsync(message).GetAwaiter().GetResult();
    }
}
