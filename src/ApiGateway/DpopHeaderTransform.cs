using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;

internal sealed class DpopHeaderTransform : ITransformProvider
{
    public void ValidateRoute(TransformRouteValidationContext context) { }

    public void ValidateCluster(TransformClusterValidationContext context) { }

    public void Apply(TransformBuilderContext context)
    {
        context.AddRequestTransform(transformContext =>
        {
            // YARP normally copies Authorization, but make the sender-constrained
            // contract explicit so native clients do not lose the access token on
            // routes that do not use the BFF session bridge.
            if (transformContext.HttpContext.Request.Headers.TryGetValue("Authorization", out var authorization))
            {
                transformContext.ProxyRequest.Headers.Remove("Authorization");
                transformContext.ProxyRequest.Headers.TryAddWithoutValidation("Authorization", authorization.ToString());
            }

            if (transformContext.HttpContext.Request.Headers.TryGetValue("DPoP", out var proof))
            {
                transformContext.ProxyRequest.Headers.Remove("DPoP");
                transformContext.ProxyRequest.Headers.TryAddWithoutValidation("DPoP", proof.ToString());
            }

            // YARP's default forwarded-header transform can be absent or can
            // describe the internal cluster origin. Resource services validate
            // the DPoP `htu` against the public gateway URL, so copy the values
            // explicitly from the original request.
            foreach (var header in new[] { "X-Forwarded-Proto", "X-Forwarded-Host" })
            {
                if (transformContext.HttpContext.Request.Headers.TryGetValue(header, out var value))
                {
                    transformContext.ProxyRequest.Headers.Remove(header);
                    transformContext.ProxyRequest.Headers.TryAddWithoutValidation(header, value.ToString());
                }
            }

            return ValueTask.CompletedTask;
        });
    }
}
