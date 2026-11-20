# Changelog

All notable changes to **Tamp.Telegram** are recorded here.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/);
versions follow [SemVer](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.2.0] - 2026-11-20 - .NET 11 in, .NET 8 and .NET 9 out

### Changed

- **Multi-targets `net10.0;net11.0`** instead of `net8.0;net9.0;net10.0` ([ADR 0015](https://github.com/tamp-build/tamp/blob/main/docs/adr/0015-target-framework-strategy.md)). .NET 11 shipped 2026-11-10 and .NET 8 and .NET 9 reached end of support the same day; the rule is to add a TFM the day a release ships and drop one the day Microsoft ends its support. `global.json` pins SDK 11.0.100.

  **If you target `net8.0` or `net9.0`, stay on the previous version** — this one carries no assets your project can resolve.

- **`Tamp.*` pinned to 1.21.0**, which carries a SARIF fix worth knowing about if this project's output is read as evidence: before 1.21.0, a SARIF result whose scanner omitted `level` was read as `none` — the least severe value — instead of the declared `warning`, on every runtime before net11 ([tamp#166](https://github.com/tamp-build/tamp/issues/166)).

- **Publishing moved to nuget.org trusted publishing (OIDC).** The `tamp_nuget_api_key` org secret expired 2026-11-01 along with every other pre-2026-08-17 nuget.org key. The publish job now mints a short-lived key from its own OIDC token, so there is no long-lived credential to rotate or leak.

## [0.1.1] — 2026-09-27

- Package now ships XML documentation files (`.xml`) alongside the assembly, so consumers get IntelliSense and API docs. (Mirrors [tamp-build/tamp#3](https://github.com/tamp-build/tamp/pull/50).)

## [0.1.0] - 2026-05-15

First public release. Telegram side of the Tamp.Notify epic (TAM-227); requires Tamp.Core ≥ 1.10.0 for the `[BuildReporter]` plug-in surface (TAM-230).

### Added

- **`TelegramBuildReporter`** — `IBuildReporter` plug-in that pings a Telegram chat when a Tamp build target fails. Markdown payload carries target name, duration, failure reason, and the last 50 lines of the target's stdout/stderr tail (populated by Tamp.Core's per-target `TargetOutputBuffer`). Successful builds stay silent.
- **`TelegramBuildReporter.FromEnvironment()`** factory — pulls credentials from `TELEGRAM_BOT_TOKEN` + `TELEGRAM_CHAT_ID` (and optional `TELEGRAM_BUILD_LABEL`). Returns `null` when either required var is missing; the framework silently skips null-valued `[BuildReporter]` members, so adopters can paste the same field declaration into every Build.cs without per-environment guards.
- **`Telegram.SendMessage(...)`** facade — top-level static surface for adopters who want to send their own messages from a target body (deploy succeeded, release published, custom signal). Pairs with the reporter for general notification needs.
- **`TelegramClient`** — `TampApiClient` subclass over the Telegram Bot API; one verb today (`sendMessage`). MarkdownV2 / HTML / plain parse modes; disable-notification + disable-link-preview knobs.
- **Output-tail cap** — failure messages auto-trim to 50 lines / 3 KB to stay under Telegram's 4 KB sendMessage limit.
- **Reporter failures are swallowed** — a flaky Telegram outage prints one stderr line via `Console.Error` and does not bubble. The framework's `CompositeBuildReporter` already isolates exceptions from one reporter to the next, but this is belt-and-suspenders so the rest of the build keeps reporting.

### Requires

- Tamp.Core `≥ 1.10.0` (for `[BuildReporter]` + `TargetFailureDetail.OutputTail`)
- Tamp.Http `≥ 0.1.1` (for the `TampApiClient` base + `ApiException` shape)
- A Telegram bot — register via `@BotFather` once; copy the HTTP API token plus the target chat-id

### Setup

```bash
# 1. Create a bot in Telegram via @BotFather → /newbot
# 2. DM the new bot once so it knows your chat-id; fetch it via:
curl -sS https://api.telegram.org/bot<TOKEN>/getUpdates | jq '.result[0].message.chat.id'

# 3. Set the env vars in your CI workflow + locally:
export TELEGRAM_BOT_TOKEN=...      # from BotFather
export TELEGRAM_CHAT_ID=...        # from getUpdates
export TELEGRAM_BUILD_LABEL=...    # optional — appears as prefix in every message
```

```csharp
// In your Build.cs:
class Build : TampBuild
{
    public static int Main(string[] args) => Execute<Build>(args);

    [BuildReporter] readonly IBuildReporter? TelegramNotify =
        TelegramBuildReporter.FromEnvironment();

    // ... your targets ...
}
```

[Unreleased]: https://github.com/tamp-build/tamp-telegram/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/tamp-build/tamp-telegram/releases/tag/v0.1.0
