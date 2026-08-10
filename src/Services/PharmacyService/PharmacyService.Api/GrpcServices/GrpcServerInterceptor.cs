using Grpc.Core;
using Grpc.Core.Interceptors;

namespace His.Hope.PharmacyService.Api.GrpcServices;

public class GrpcServerInterceptor : Interceptor
{
    private readonly ILogger<GrpcServerInterceptor> _logger;

    public GrpcServerInterceptor(ILogger<GrpcServerInterceptor> logger) =>
        _logger = logger;

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        var method = context.Method;
        _logger.LogInformation("gRPC call started: {Method}", method);

        try
        {
            var response = await continuation(request, context);
            _logger.LogInformation("gRPC call completed: {Method}", method);
            return response;
        }
        catch (RpcException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "gRPC call failed: {Method}", method);
            throw new RpcException(new Status(StatusCode.Internal, ex.Message));
        }
    }
}
