using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrderHub.Infrastructure.Gemini;

namespace OrderHub.Tests;

public class GeminiInteractionsClientTests
{
    [Fact]
    public async Task GenerateJsonAsync_ReturnsMockedModelOutputAndLogsResponse()
    {
        const string expectedResult = "{\"intent\":\"search\",\"status\":\"Cancelled\",\"memberTier\":\"Gold\",\"dateFrom\":\"2026-06-01\",\"dateTo\":\"2026-06-30\"}";
        var handler = new StubHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("This response must not be used.")
            });
        var logger = new RecordingLogger<GeminiInteractionsClient>();
        var options = Options.Create(new GeminiOptions
        {
            ApiKey = "test-api-key",
            MaxRetries = 0
        });
        var client = new GeminiInteractionsClient(new HttpClient(handler), options, logger);

        var result = await client.GenerateJsonAsync("input", "{}", CancellationToken.None);

        var log = Assert.Single(logger.Entries);
        Assert.Equal(expectedResult, result);
        Assert.Equal(LogLevel.Information, log.Level);
        Assert.Equal(
            ["HttpResponseCode", "ResponseMessage"],
            log.Properties
                .Where(property => property.Key != "{OriginalFormat}")
                .Select(property => property.Key));
        Assert.Equal(200, log.Properties.Single(property => property.Key == "HttpResponseCode").Value);
        var responseMessage = Assert.IsType<string>(
            log.Properties.Single(property => property.Key == "ResponseMessage").Value);
        using var responseJson = JsonDocument.Parse(responseMessage);
        Assert.Equal(
            expectedResult,
            responseJson.RootElement
                .GetProperty("steps")[0]
                .GetProperty("content")[0]
                .GetProperty("text")
                .GetString());
        Assert.DoesNotContain("test-api-key", log.RenderedMessage);
    }

    private sealed class StubHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(response);
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var properties = (IEnumerable<KeyValuePair<string, object?>>)state!;
            Entries.Add(new LogEntry(
                logLevel,
                properties.ToList(),
                formatter(state, exception)));
        }
    }

    private sealed record LogEntry(
        LogLevel Level,
        IReadOnlyList<KeyValuePair<string, object?>> Properties,
        string RenderedMessage);
}
