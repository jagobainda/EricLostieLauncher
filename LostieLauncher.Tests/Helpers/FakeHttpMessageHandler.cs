using System.Text;

namespace LostieLauncher.Tests.Helpers;

public sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly List<Func<HttpRequestMessage, HttpResponseMessage?>> _matchers = [];

    public List<HttpRequestMessage> Requests { get; } = [];

    public Func<HttpRequestMessage, HttpResponseMessage> DefaultResponder { get; set; } =
        _ => new HttpResponseMessage(HttpStatusCode.NotFound);

    public FakeHttpMessageHandler RespondWithJson(
        string urlSubstring,
        string json,
        HttpStatusCode statusCode = HttpStatusCode.OK,
        HttpMethod? method = null)
    {
        _matchers.Add(req =>
        {
            if (method is not null && req.Method != method) return null;
            if (req.RequestUri is null || !req.RequestUri.ToString().Contains(urlSubstring, StringComparison.OrdinalIgnoreCase)) return null;

            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        });
        return this;
    }

    public FakeHttpMessageHandler RespondWithStatus(
        string urlSubstring,
        HttpStatusCode statusCode,
        HttpMethod? method = null)
    {
        _matchers.Add(req =>
        {
            if (method is not null && req.Method != method) return null;
            if (req.RequestUri is null || !req.RequestUri.ToString().Contains(urlSubstring, StringComparison.OrdinalIgnoreCase)) return null;

            return new HttpResponseMessage(statusCode);
        });
        return this;
    }

    public FakeHttpMessageHandler Respond(Func<HttpRequestMessage, HttpResponseMessage?> matcher)
    {
        _matchers.Add(matcher);
        return this;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);

        foreach (var matcher in _matchers)
        {
            var response = matcher(request);
            if (response is not null) return Task.FromResult(response);
        }

        return Task.FromResult(DefaultResponder(request));
    }
}
