namespace Tamp.Telegram;

/// <summary>
/// Construction-time settings for <see cref="TelegramClient"/>. Owns the
/// one place that turns the raw bot-token <see cref="Secret"/> into a
/// usable URL — and because the class name ends in <c>Settings</c>, it
/// counts as an approved context for the TAMP004 analyzer's
/// <see cref="Secret.Reveal"/> rule. (The Bot API authenticates by
/// embedding the token in the URL path rather than via an
/// <c>Authorization</c> header, so the satellite needs cleartext
/// access at exactly one point: base-URL construction.)
/// </summary>
internal static class TelegramClientSettings
{
    private const string TelegramApiHost = "https://api.telegram.org";

    /// <summary>
    /// Compose <c>https://api.telegram.org/bot&lt;TOKEN&gt;/</c> for use as
    /// <see cref="System.Net.Http.HttpClient.BaseAddress"/>. Trailing
    /// slash is mandatory — <see cref="System.Net.Http.HttpClient"/>'s
    /// relative-URI combination drops the last segment without it.
    /// </summary>
    public static System.Uri BuildBaseUri(Secret botToken)
        => new($"{TelegramApiHost}/bot{botToken.Reveal()}/");
}
