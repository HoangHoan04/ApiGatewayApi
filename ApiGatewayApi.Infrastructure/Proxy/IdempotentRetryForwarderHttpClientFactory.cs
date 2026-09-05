using Yarp.ReverseProxy.Forwarder;

namespace ApiGatewayApi.Infrastructure.Proxy;

/// <summary>
/// Retries GET/HEAD/OPTIONS on transport failure or 502/503/504. Never retries mutating methods.
/// </summary>
public sealed class IdempotentRetryForwarderHttpClientFactory : ForwarderHttpClientFactory
{
    protected override HttpMessageHandler WrapHandler(ForwarderHttpClientContext context, HttpMessageHandler handler)
    {
        return new IdempotentRetryHandler { InnerHandler = handler };
    }
}

internal sealed class IdempotentRetryHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return CreateClientClosedResponse(request);
        }

        bool canRetry = IsIdempotent(request);
        const int maxAttempts = 3;
        HttpResponseMessage? last = null;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            last?.Dispose();
            try
            {
                last = await base.SendAsync(request, cancellationToken);
                int code = (int)last.StatusCode;
                bool retryable = code is 408 or 502 or 503 or 504;
                if (!canRetry || attempt == maxAttempts || !retryable)
                {
                    return last;
                }
            }
            catch (HttpRequestException ex)
            {
                if (canRetry && attempt < maxAttempts && !cancellationToken.IsCancellationRequested)
                {
                    last = null;
                }
                else
                {
                    return CreateBadGatewayResponse(request, ex.Message);
                }
            }
            catch (OperationCanceledException ex)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return CreateClientClosedResponse(request);
                }

                if (canRetry && attempt < maxAttempts)
                {
                    last = null;
                }
                else
                {
                    return CreateGatewayTimeoutResponse(request, ex.Message);
                }
            }

            try
            {
                await Task.Delay(40 * attempt, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return CreateClientClosedResponse(request);
            }
        }

        return last ?? CreateBadGatewayResponse(request, "Retry exhausted or downstream service unavailable.");
    }

    private static HttpResponseMessage CreateClientClosedResponse(HttpRequestMessage request)
    {
        return new HttpResponseMessage((System.Net.HttpStatusCode)499)
        {
            RequestMessage = request,
            Content = new StringContent(
                $$"""{"type":"https://httpstatuses.com/499","title":"Client Closed Request","status":499,"detail":"Yêu cầu bị hủy do client ngắt kết nối."}""",
                System.Text.Encoding.UTF8,
                "application/json")
        };
    }

    private static HttpResponseMessage CreateBadGatewayResponse(HttpRequestMessage request, string reason)
    {
        return new HttpResponseMessage(System.Net.HttpStatusCode.BadGateway)
        {
            RequestMessage = request,
            Content = new StringContent(
                $$"""{"type":"https://datatracker.ietf.org/doc/html/rfc7231#section-6.6.3","title":"Bad Gateway","status":502,"detail":"Dịch vụ downstream tạm thời không hoạt động hoặc từ chối kết nối."}""",
                System.Text.Encoding.UTF8,
                "application/json")
        };
    }

    private static HttpResponseMessage CreateGatewayTimeoutResponse(HttpRequestMessage request, string reason)
    {
        return new HttpResponseMessage(System.Net.HttpStatusCode.GatewayTimeout)
        {
            RequestMessage = request,
            Content = new StringContent(
                $$"""{"type":"https://datatracker.ietf.org/doc/html/rfc7231#section-6.6.5","title":"Gateway Timeout","status":504,"detail":"Dịch vụ downstream phản hồi quá thời gian cho phép."}""",
                System.Text.Encoding.UTF8,
                "application/json")
        };
    }

    private static bool IsIdempotent(HttpRequestMessage request)
    {
        return request.Content is null &&
        (request.Method == HttpMethod.Get ||
         request.Method == HttpMethod.Head ||
         request.Method == HttpMethod.Options);
    }
}
