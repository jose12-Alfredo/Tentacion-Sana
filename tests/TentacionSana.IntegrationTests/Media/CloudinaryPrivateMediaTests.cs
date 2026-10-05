using System.Net;
using System.Net.Http.Headers;
using TentacionSana.Infrastructure.Media;

namespace TentacionSana.IntegrationTests.Media;

public sealed class CloudinaryPrivateMediaTests
{
    [Fact]
    public async Task DownloadAsyncFallsBackToPrivateEndpointWhenSignedCdnRejectsRequest()
    {
        var handler = new SequenceHandler(
            new HttpResponseMessage(HttpStatusCode.Unauthorized),
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent([1, 2, 3])
                {
                    Headers = { ContentType = new MediaTypeHeaderValue("image/png") }
                }
            });
        using var client = new HttpClient(handler);
        var factory = new TestHttpClientFactory(client);
        var settings = new CloudinaryOptions
        {
            CloudName = "test-cloud",
            ApiKey = "123456",
            ApiSecret = "test-secret"
        };

        var result = await CloudinaryPrivateMedia.DownloadAsync(settings, factory,
            "tentacion-sana/evidence/sample", "png", "sample.png",
            DateTimeOffset.UtcNow.AddMinutes(5), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal([1, 2, 3], result.Value.Content);
        Assert.Equal("image/png", result.Value.ContentType);
        Assert.Collection(handler.Requests,
            request =>
            {
                Assert.Equal("res.cloudinary.com", request.Host);
                Assert.Contains("/image/authenticated/", request.AbsolutePath, StringComparison.Ordinal);
            },
            request => Assert.Equal("api.cloudinary.com", request.Host));
    }

    private sealed class TestHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class SequenceHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);
        public List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri ?? throw new InvalidOperationException("Request URI is required."));
            return Task.FromResult(_responses.Dequeue());
        }
    }
}
