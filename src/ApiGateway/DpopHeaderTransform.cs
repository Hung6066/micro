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
            if (transformContext.HttpContext.Request.Headers.TryGetValue("DPoP", out var proof))
            {
                transformContext.ProxyRequest.Headers.Remove("DPoP");
                transformContext.ProxyRequest.Headers.TryAddWithoutValidation("DPoP", proof.ToString());
            }

            return ValueTask.CompletedTask;
        });
    }
}
