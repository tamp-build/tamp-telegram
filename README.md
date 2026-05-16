# Tamp.Telegram

Send Tamp build outcomes to Telegram via the Bot API.

Two pieces:
- **`TelegramBuildReporter`** — `IBuildReporter` plug-in that pings a Telegram chat whenever a target fails (with the last 50 lines of stdout for context, courtesy of TAM-230's per-target output buffer). Wire into your `Build.cs` via `[BuildReporter]` from Tamp.Core 1.10.0+.
- **`Telegram.SendMessage(...)`** — top-level facade for adopters who want to send their own messages (deploy succeeded, release published, custom signal) from a target body.

## Quick start

```csharp
class Build : TampBuild
{
    public static int Main(string[] args) => Execute<Build>(args);

    // Reads TELEGRAM_BOT_TOKEN + TELEGRAM_CHAT_ID env vars; returns null
    // (silently skipped by the framework) when either is missing.
    [BuildReporter] readonly IBuildReporter TelegramNotify =
        TelegramBuildReporter.FromEnvironment();

    // ... your targets ...
}
```

Fires on every target failure + on build-end when the build went red. Successful builds stay silent.

## License

MIT.
