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

            return ValueTask.CompletedTask;
        });
    }
}
