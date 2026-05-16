using Xunit;

namespace Tamp.Telegram.Tests;

public sealed class TelegramBuildReporterTests
{
    [Fact]
    public void FromEnvironment_Returns_Null_When_Token_Missing()
    {
        using var _ = EnvScope.Set("TELEGRAM_BOT_TOKEN", null);
        using var __ = EnvScope.Set("TELEGRAM_CHAT_ID", "8698669845");

        Assert.Null(TelegramBuildReporter.FromEnvironment());
    }

    [Fact]
    public void FromEnvironment_Returns_Null_When_Chat_Id_Missing()
    {
        using var _ = EnvScope.Set("TELEGRAM_BOT_TOKEN", "token");
        using var __ = EnvScope.Set("TELEGRAM_CHAT_ID", null);

        Assert.Null(TelegramBuildReporter.FromEnvironment());
    }

    [Fact]
    public void FromEnvironment_Constructs_When_Both_Vars_Present()
    {
        using var _ = EnvScope.Set("TELEGRAM_BOT_TOKEN", "token");
        using var __ = EnvScope.Set("TELEGRAM_CHAT_ID", "8698669845");

        var reporter = TelegramBuildReporter.FromEnvironment();
        Assert.NotNull(reporter);
    }

    [Fact]
    public void Constructor_Rejects_Empty_Chat_Id()
    {
        Assert.Throws<System.ArgumentException>(() =>
            new TelegramBuildReporter(new Secret("t", "token"), ""));
    }

    [Fact]
    public void Constructor_Rejects_Null_Bot_Token()
    {
        Assert.Throws<System.ArgumentNullException>(() =>
            new TelegramBuildReporter(null!, "8698669845"));
    }

    /// <summary>
    /// IDisposable env-var scope so tests don't leak state into each other or
    /// into the process for parallel runners.
    /// </summary>
    private sealed class EnvScope : System.IDisposable
    {
        private readonly string _name;
        private readonly string? _previous;

        private EnvScope(string name, string? previous) { _name = name; _previous = previous; }

        public static EnvScope Set(string name, string? value)
        {
            var prev = System.Environment.GetEnvironmentVariable(name);
            System.Environment.SetEnvironmentVariable(name, value);
            return new EnvScope(name, prev);
        }

        public void Dispose() => System.Environment.SetEnvironmentVariable(_name, _previous);
    }
}
