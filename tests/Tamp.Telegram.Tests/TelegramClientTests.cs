using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Tamp.Telegram.Tests;

public sealed class TelegramClientTests
{
    [Fact]
    public async Task SendMessage_Posts_To_Telegram_With_Bot_Token_In_Url()
    {
        var (handler, captured) = RecordingHandler.WithOkResponse(messageId: 17, chatId: 8698669845, date: 1700000000);
        var http = new HttpClient(handler);
        using var client = new TelegramClient(new Secret("token", "ABC123"), http: http);

        var result = await client.SendMessageAsync(new TelegramMessage("8698669845", "hello"));

        Assert.Equal(17, result.MessageId);
        Assert.Equal(8698669845, result.ChatId);
        Assert.Equal("https://api.telegram.org/botABC123/sendMessage", captured.RequestUri!.ToString());
        Assert.Equal(HttpMethod.Post, captured.Method);
    }

    [Fact]
    public async Task SendMessage_Serializes_Snake_Case_Wire_Fields()
    {
        var handler = RecordingHandler.Capturing(messageId: 1, chatId: 1, date: 1);
        var http = new HttpClient(handler);
        using var client = new TelegramClient(new Secret("token", "T"), http: http);

        await client.SendMessageAsync(new TelegramMessage("chat", "hi") { ParseMode = TelegramParseMode.MarkdownV2 });

        Assert.NotNull(handler.CapturedBody);
        using var doc = JsonDocument.Parse(handler.CapturedBody!);
        Assert.Equal("chat", doc.RootElement.GetProperty("chat_id").GetString());
        Assert.Equal("hi", doc.RootElement.GetProperty("text").GetString());
        Assert.Equal("MarkdownV2", doc.RootElement.GetProperty("parse_mode").GetString());
    }

    [Fact]
    public async Task SendMessage_Throws_ApiException_When_Bot_Returns_OkFalse()
    {
        var (handler, _) = RecordingHandler.WithRawJson(
            "{\"ok\":false,\"description\":\"chat not found\",\"error_code\":400}");
        var http = new HttpClient(handler);
        using var client = new TelegramClient(new Secret("token", "T"), http: http);

        await Assert.ThrowsAsync<Tamp.Http.ApiException>(() =>
            client.SendMessageAsync(new TelegramMessage("missing-chat", "x")));
    }

    [Fact]
    public void Null_Bot_Token_Throws_At_Construction()
    {
        Assert.Throws<System.ArgumentNullException>(() => new TelegramClient(null!));
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly System.Func<HttpRequestMessage, HttpResponseMessage> _responder;
        public HttpRequestMessage? LastRequest { get; private set; }
        /// <summary>Request body captured eagerly during SendAsync — survives the using-var disposal in TampApiClient.</summary>
        public string? CapturedBody { get; private set; }

        private RecordingHandler(System.Func<HttpRequestMessage, HttpResponseMessage> responder) { _responder = responder; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            if (request.Content is not null)
            {
                CapturedBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }
            return _responder(request);
        }

        public static (RecordingHandler, HttpRequestMessage) WithOkResponse(long messageId, long chatId, long date)
        {
            var captured = new HttpRequestMessage();
            var handler = new RecordingHandler(req =>
            {
                captured.Method = req.Method;
                captured.RequestUri = req.RequestUri;
                captured.Content = req.Content;
                var body = "{\"ok\":true,\"result\":{\"message_id\":" + messageId
                    + ",\"date\":" + date
                    + ",\"chat\":{\"id\":" + chatId + "}}}";
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
                };
            });
            return (handler, captured);
        }

        public static (RecordingHandler, HttpRequestMessage) WithRawJson(string body)
        {
            var captured = new HttpRequestMessage();
            var handler = new RecordingHandler(req =>
            {
                captured.Method = req.Method;
                captured.RequestUri = req.RequestUri;
                captured.Content = req.Content;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
                };
            });
            return (handler, captured);
        }

        public static RecordingHandler Capturing(long messageId, long chatId, long date)
        {
            return new RecordingHandler(req =>
            {
                var body = "{\"ok\":true,\"result\":{\"message_id\":" + messageId
                    + ",\"date\":" + date
                    + ",\"chat\":{\"id\":" + chatId + "}}}";
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
                };
            });
        }
    }
}
