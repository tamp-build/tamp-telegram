using System.Collections.Generic;

namespace Tamp.Telegram;

/// <summary>
/// <see cref="IBuildReporter"/> that pings a Telegram chat when a Tamp
/// build target fails. Designed to be registered on a <see cref="TampBuild"/>
/// via the framework's <see cref="BuildReporterAttribute"/>:
///
/// <code>
/// [BuildReporter] readonly IBuildReporter TelegramNotify =
///     TelegramBuildReporter.FromEnvironment();
/// </code>
///
/// Two events fire a message:
/// <list type="bullet">
///   <item><b>Target failure</b> — markdown payload with target name, duration,
///         failure reason, and the last 50 lines of the target's stdout/stderr
///         tail (TAM-230 — provided by Tamp.Core's <see cref="TargetFailureDetail"/>).</item>
///   <item><b>Build end</b> — only when the build ended in failure. Summary line
///         with the first-failed target and total wall-clock duration.</item>
/// </list>
/// Successful builds are intentionally silent — most adopters only want to
/// hear about red builds. The "notify on success too" flag is a follow-up.
/// </summary>
public sealed class TelegramBuildReporter : IBuildReporter
{
    private const int MaxStdoutTailLines = 50;
    private const int MaxStdoutTailChars = 3000;   // Telegram caps sendMessage text at 4096; leave room for the rest.

    private readonly TelegramClient _client;
    private readonly string _chatId;
    private readonly string? _buildLabel;
    private bool _sentTargetFailure;

    /// <summary>
    /// Construct a reporter with explicit credentials. Use
    /// <see cref="FromEnvironment"/> for the env-var-driven path adopters
    /// typically reach for.
    /// </summary>
    /// <param name="botToken">Telegram bot HTTP API token from BotFather.</param>
    /// <param name="chatId">Target chat / channel id (numeric, possibly negative for groups).</param>
    /// <param name="buildLabel">Optional human-readable label (e.g. <c>"tamp-beacon"</c> or <c>"main-ci"</c>) prepended to every message so adopters notifying from multiple builds can distinguish them.</param>
    public TelegramBuildReporter(Secret botToken, string chatId, string? buildLabel = null)
    {
        if (string.IsNullOrWhiteSpace(chatId)) throw new System.ArgumentException("chatId is required", nameof(chatId));
        _client = new TelegramClient(botToken ?? throw new System.ArgumentNullException(nameof(botToken)));
        _chatId = chatId;
        _buildLabel = buildLabel;
    }

    /// <summary>
    /// Convenience factory: pull credentials from the canonical env vars
    /// <c>TELEGRAM_BOT_TOKEN</c> + <c>TELEGRAM_CHAT_ID</c> (and the
    /// optional <c>TELEGRAM_BUILD_LABEL</c>). Returns <c>null</c> when
    /// either required var is missing — adopters wire this in via a field
    /// initializer + <see cref="BuildReporterAttribute"/>, and the
    /// framework silently skips null-valued reporters during collection.
    /// </summary>
    public static TelegramBuildReporter? FromEnvironment()
    {
        var token = System.Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN");
        var chatId = System.Environment.GetEnvironmentVariable("TELEGRAM_CHAT_ID");
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(chatId)) return null;
        var label = System.Environment.GetEnvironmentVariable("TELEGRAM_BUILD_LABEL");
        return new TelegramBuildReporter(new Secret("TELEGRAM_BOT_TOKEN", token), chatId, label);
    }

    public void OnBuildStart(string buildId, IReadOnlyList<string> requestedTargets, IReadOnlyList<string> executionClosure) { }
    public void OnTargetStart(string name) { }
    public void OnTargetSucceeded(string name, System.TimeSpan duration) { }
    public void OnTargetSkipped(string name, string reason) { }
    public void OnTargetNotRun(string name, string reason) { }

    public void OnTargetFailed(TargetFailureDetail detail)
    {
        _sentTargetFailure = true;
        var text = BuildFailureText(detail);
        TrySend(text);
    }

    public void OnBuildEnd(string status, string? firstFailedTarget, int exitCode, System.TimeSpan totalDuration)
    {
        // Only ping on red builds — green builds stay silent. Skip if the
        // target-failure handler already fired (avoid double-ping for a
        // single-target failure).
        if (status == "succeeded") return;
        if (_sentTargetFailure && firstFailedTarget is not null) return;

        var label = string.IsNullOrEmpty(_buildLabel) ? "Tamp build" : _buildLabel!;
        var sb = new System.Text.StringBuilder();
        sb.Append("🛑 ").Append(label).Append(" — build ").Append(status);
        if (firstFailedTarget is not null) sb.Append(" (first failed: ").Append(firstFailedTarget).Append(')');
        sb.Append('\n').Append("Exit: ").Append(exitCode);
        sb.Append("  •  Duration: ").Append(FormatDuration(totalDuration));
        TrySend(sb.ToString());
    }

    /// <summary>
    /// Send a plain-text test ping. Exposed so adopters can wire a smoke
    /// target in their Build.cs (<c>tamp NotifySmoke</c>) to confirm the
    /// bot token + chat id are correctly configured.
    /// </summary>
    public TelegramMessageResult Ping(string text = "Tamp.Telegram smoke ping ✅")
        => _client.SendMessageAsync(new TelegramMessage(_chatId, text)).GetAwaiter().GetResult();

    private string BuildFailureText(TargetFailureDetail detail)
    {
        var label = string.IsNullOrEmpty(_buildLabel) ? "Tamp build" : _buildLabel!;
        var sb = new System.Text.StringBuilder();
        sb.Append("❌ ").Append(label).Append(" — target FAILED: ").Append(detail.TargetName).Append('\n');
        sb.Append("Reason: ").Append(detail.FailureReason).Append('\n');
        sb.Append("Duration: ").Append(FormatDuration(detail.Duration));
        if (detail.OutputTail.Count > 0)
        {
            sb.Append("\n\n--- last ").Append(System.Math.Min(detail.OutputTail.Count, MaxStdoutTailLines)).Append(" lines ---\n");
            sb.Append(TrimTail(detail.OutputTail));
        }
        return sb.ToString();
    }

    private static string TrimTail(IReadOnlyList<string> tail)
    {
        // Cap by line count AND total chars — Telegram rejects messages > 4096 chars.
        var start = System.Math.Max(0, tail.Count - MaxStdoutTailLines);
        var sb = new System.Text.StringBuilder();
        for (var i = start; i < tail.Count; i++)
        {
            if (sb.Length + tail[i].Length + 1 > MaxStdoutTailChars)
            {
                sb.Append("…[truncated]");
                break;
            }
            sb.Append(tail[i]).Append('\n');
        }
        return sb.ToString().TrimEnd('\n');
    }

    private static string FormatDuration(System.TimeSpan d)
    {
        if (d.TotalSeconds < 1) return $"{d.TotalMilliseconds:F0}ms";
        if (d.TotalMinutes < 1) return $"{d.TotalSeconds:F1}s";
        if (d.TotalHours < 1) return $"{d.Minutes}m {d.Seconds}s";
        return $"{(int)d.TotalHours}h {d.Minutes}m";
    }

    private void TrySend(string text)
    {
        try
        {
            _client.SendMessageAsync(new TelegramMessage(_chatId, text)).GetAwaiter().GetResult();
        }
        catch (System.Exception ex)
        {
            // Reporters must never bubble — the framework's CompositeBuildReporter
            // already isolates exceptions, but be explicit here so a flaky
            // Telegram outage doesn't even surface a stderr line if the
            // network is the cause. Adopters who want loud-on-failure can
            // wrap the reporter in a logging decorator.
            System.Console.Error.WriteLine($"[tamp-telegram] sendMessage failed: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
