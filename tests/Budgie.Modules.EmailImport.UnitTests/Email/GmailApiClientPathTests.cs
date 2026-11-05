using System.Net;
using System.Text;
using Budgie.Modules.EmailImport.Application.Abstractions;
using Budgie.Modules.EmailImport.Domain.Connections;
using Budgie.Modules.EmailImport.Infrastructure.Email.Gmail;
using Budgie.Shared.Enums;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Budgie.Modules.EmailImport.UnitTests.Email;

/// <summary>
/// Regression tests for the review findings that a fake-based test could never catch:
/// the Gmail REST paths and the mailbox's current historyId.
/// </summary>
public class GmailApiClientPathTests
{
    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<string> RequestedPaths { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestedPaths.Add(request.RequestUri!.PathAndQuery);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json"),
            });
        }
    }

    private static (GmailApiClient Client, RecordingHandler Handler) CreateClient()
    {
        var handler = new RecordingHandler();
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://gmail.googleapis.com/gmail/v1/") };
        var options = Microsoft.Extensions.Options.Options.Create(new GmailOptions
        {
            ClientId = "id",
            ClientSecret = "secret",
        });

        return (new GmailApiClient(http, options, NullLogger<GmailApiClient>.Instance), handler);
    }

    // The Gmail REST API is gmail/v1/users/{userId}/<resource>; "me" is the alias for the
    // authenticated mailbox. "users/messages/list" is NOT a valid path and returns 404
    // against the real API — the RPC-style method names are not URLs.
    [Fact]
    public async Task ListMessageIdsAsync_UsesTheMeSegment_NotTheRpcStylePath()
    {
        var (client, handler) = CreateClient();

        await client.ListMessageIdsAsync("token", "newer_than:7d", CancellationToken.None);

        handler.RequestedPaths.Should().ContainSingle();
        handler.RequestedPaths[0].Should().StartWith("/gmail/v1/users/me/messages?");
        handler.RequestedPaths[0].Should().NotContain("/list");
        handler.RequestedPaths[0].Should().Contain("q=newer_than");
    }

    [Fact]
    public async Task ListHistoryAsync_UsesTheMeSegment()
    {
        var (client, handler) = CreateClient();

        await client.ListHistoryAsync("token", "12345", CancellationToken.None);

        handler.RequestedPaths.Should().ContainSingle();
        handler.RequestedPaths[0].Should().StartWith("/gmail/v1/users/me/history?");
        handler.RequestedPaths[0].Should().NotContain("/list");
        handler.RequestedPaths[0].Should().Contain("startHistoryId=12345");
    }

    [Fact]
    public async Task GetProfileAsync_UsesTheMeSegment()
    {
        var (client, handler) = CreateClient();

        await client.GetProfileAsync("token", CancellationToken.None);

        handler.RequestedPaths.Should().ContainSingle();
        handler.RequestedPaths[0].Should().Be("/gmail/v1/users/me/profile");
    }

    [Fact]
    public async Task GetMessageAsync_UsesTheMeSegmentAndFullFormat()
    {
        var (client, handler) = CreateClient();

        await client.GetMessageAsync("token", "msg-42", CancellationToken.None);

        handler.RequestedPaths.Should().ContainSingle();
        handler.RequestedPaths[0].Should().StartWith("/gmail/v1/users/me/messages/msg-42");
        handler.RequestedPaths[0].Should().Contain("format=full");
    }

    // Guards the exact regression that shipped in the 2026-10 image: every Gmail call went
    // out with the RPC method name transcribed into the path ("messages/list",
    // "history/list", "getProfile"), so the list call answered HTTP 400 and the profile 404
    // and no message was ever fetched.
    [Fact]
    public async Task NoCall_TranscribesTheRpcMethodNameIntoThePath()
    {
        var (client, handler) = CreateClient();

        await client.ListMessageIdsAsync("token", "newer_than:7d", CancellationToken.None);
        await client.ListHistoryAsync("token", "12345", CancellationToken.None);
        await client.GetProfileAsync("token", CancellationToken.None);
        await client.GetMessageAsync("token", "msg-42", CancellationToken.None);

        handler.RequestedPaths.Should().HaveCount(4);
        handler.RequestedPaths.Should().OnlyContain(p => p.StartsWith("/gmail/v1/users/me/"));
        handler.RequestedPaths.Should().NotContain(p => p.Contains("/list"));
        handler.RequestedPaths.Should().NotContain(p => p.Contains("getProfile"));
    }
}
