using System.Net;
using System.Text;

namespace SemanticMapper.Jev.Tests;

public sealed class StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
{
    public StubHttpMessageHandler(HttpStatusCode status, string body, string? requestId = null)
        : this((_, _) =>
        {
            var response = new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
            if (requestId is not null)
            {
                response.Headers.Add("x-request-id", requestId);
            }

            return Task.FromResult(response);
        })
    {
    }

    public List<(HttpRequestMessage Request, string Body)> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add((request, body));
        return await respond(request, cancellationToken);
    }
}
