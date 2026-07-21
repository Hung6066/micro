using His.Hope.Bff.Core.Aggregation;
using His.Hope.BillingGrpc;

namespace BillingBff.Aggregation;

public sealed class InvoiceDetailedHandler : IAggregationHandler
{
    public string Route => "/api/v1/invoices/{id}/detailed";
    public string Method => "GET";

    private readonly BillingGrpcService.BillingGrpcServiceClient _billingClient;

    public InvoiceDetailedHandler(BillingGrpcService.BillingGrpcServiceClient billingClient)
    {
        _billingClient = billingClient;
    }

    public async Task<AggregationResult> HandleAsync(AggregationContext context)
    {
        var invoiceId = context.RouteValues["id"]!;
        var invoice = await _billingClient.GetInvoiceAsync(
            new InvoiceRequest { Id = invoiceId }, cancellationToken: context.CancellationToken);

        return AggregationResult.Success(new { invoice = invoice });
    }
}
