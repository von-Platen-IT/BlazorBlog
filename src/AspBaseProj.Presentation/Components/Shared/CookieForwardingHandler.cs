using Microsoft.AspNetCore.Http;

namespace AspBaseProj.Presentation.Components.Shared;

public class CookieForwardingHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var ctx = httpContextAccessor.HttpContext;
        if (ctx is not null && ctx.Request.Headers.TryGetValue("Cookie", out var cookies))
        {
            request.Headers.TryAddWithoutValidation("Cookie", cookies.ToString());
        }

        var response = await base.SendAsync(request, cancellationToken);

        // Forward Set-Cookie headers back to the client
        if (response.Headers.TryGetValues("Set-Cookie", out var setCookies))
        {
            foreach (var cookie in setCookies)
                ctx?.Response.Headers.Append("Set-Cookie", cookie);
        }

        return response;
    }
}