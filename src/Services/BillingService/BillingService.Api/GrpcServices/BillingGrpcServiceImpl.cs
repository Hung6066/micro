using AutoMapper;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using His.Hope.BillingGrpc;
using His.Hope.BillingService.Domain.Repositories;
using His.Hope.BillingService.Domain.ValueObjects;
using His.Hope.BillingService.Domain.Aggregates;
using His.Hope.BillingService.Application.DTOs;
using His.Hope.BillingService.Application.UseCases.Invoices.Queries;
using His.Hope.BillingService.Infrastructure.Persistence;
using His.Hope.Authorization;
using His.Hope.SharedKernel.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;

namespace His.Hope.BillingService.Api.GrpcServices;

[Authorize(Policy = AuthorizationPolicyNames.Permissions.BillingView)]
public class BillingGrpcServiceImpl : BillingGrpcService.BillingGrpcServiceBase
{
    private readonly IMediator _mediator;
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IMapper _mapper;
    private readonly BillingDbContext _db;
    private readonly IResourceAuthorizationEvaluator _authorization;

    public BillingGrpcServiceImpl(
        IMediator mediator,
        IInvoiceRepository invoiceRepository,
        IMapper mapper,
        BillingDbContext db,
        IResourceAuthorizationEvaluator authorization)
    {
        _mediator = mediator;
        _invoiceRepository = invoiceRepository;
        _mapper = mapper;
        _db = db;
        _authorization = authorization;
    }

    public override async Task<InvoiceResponse> GetInvoice(InvoiceRequest request,
        ServerCallContext context)
    {
        var invoiceId = InvoiceId.From(ParseGuidOrThrow(request.Id, "Invoice id"));
        await EnsureResourceAccessAsync(invoice => invoice.Id == invoiceId, invoiceId.Value.ToString("D"), context);
        var invoice = await _invoiceRepository.GetByIdAsync(invoiceId);

        if (invoice is null)
            throw new RpcException(new Status(StatusCode.NotFound, "Invoice not found"));

        return MapToResponse(invoice);
    }

    public override async Task<InvoiceResponse> GetInvoiceByNumber(
        InvoiceByNumberRequest request, ServerCallContext context)
    {
        await EnsureResourceAccessAsync(invoice => invoice.InvoiceNumber == request.InvoiceNumber, request.InvoiceNumber, context);
        var invoice = await _invoiceRepository.GetByInvoiceNumberAsync(request.InvoiceNumber);

        if (invoice is null)
            throw new RpcException(new Status(StatusCode.NotFound, "Invoice not found"));

        return MapToResponse(invoice);
    }

    public override async Task<InvoiceListResponse> GetPatientInvoices(
        PatientInvoicesRequest request, ServerCallContext context)
    {
        var patientId = ParseGuidOrThrow(request.PatientId, "Patient id");
        var page = request.Page > 0 ? request.Page : 1;
        var pageSize = request.PageSize > 0 ? request.PageSize : 20;
        var accessScope = FacilityAccessScope.FromPrincipal(context.GetHttpContext().User);

        var allInvoices = await _invoiceRepository.GetByPatientAsync(patientId,
            accessScope.FacilityIds, accessScope.IsCrossFacility, context.CancellationToken);
        var totalCount = allInvoices.Count;
        var paged = allInvoices
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(MapToResponse);

        var response = new InvoiceListResponse();
        response.Invoices.AddRange(paged);
        response.TotalCount = totalCount;
        response.Page = page;
        response.PageSize = pageSize;
        return response;
    }

    public override async Task<InvoiceExistsResponse> CheckInvoiceExists(
        InvoiceExistsRequest request, ServerCallContext context)
    {
        var invoiceId = InvoiceId.From(ParseGuidOrThrow(request.Id, "Invoice id"));
        var invoice = await _invoiceRepository.GetByIdAsync(invoiceId);

        return new InvoiceExistsResponse { Exists = invoice is not null };
    }

    private async Task EnsureResourceAccessAsync(
        System.Linq.Expressions.Expression<Func<Invoice, bool>> predicate,
        string canonicalId,
        ServerCallContext context)
    {
        var decision = await _authorization.EvaluateResourceAsync(
            _db.Invoices,
            predicate,
            invoice => invoice.FacilityId,
            context.GetHttpContext().User,
            HisHopePermissions.Billing.View,
            "invoice",
            canonicalId,
            context.CancellationToken);
        if (!decision.Allowed)
            throw new RpcException(new Status(StatusCode.NotFound, "Invoice not found"));
    }

    public override async Task<InvoiceListResponse> SearchInvoices(
        InvoiceSearchRequest request, ServerCallContext context)
    {
        var page = request.Page > 0 ? request.Page : 1;
        var pageSize = request.PageSize > 0 ? request.PageSize : 20;
        var accessScope = FacilityAccessScope.FromPrincipal(context.GetHttpContext().User);
        var result = await _invoiceRepository.SearchAsync(
            request.SearchTerm, (int)page, (int)pageSize, null, null, null, null,
            accessScope.FacilityIds, accessScope.IsCrossFacility, context.CancellationToken);

        var response = new InvoiceListResponse();
        response.Invoices.AddRange(result.Items.Select(MapToResponse));
        response.TotalCount = result.TotalCount;
        response.Page = page;
        response.PageSize = pageSize;
        return response;
    }

    private static Guid ParseGuidOrThrow(string value, string fieldName)
    {
        if (Guid.TryParse(value, out var parsed))
            return parsed;

        throw new RpcException(new Status(StatusCode.InvalidArgument, $"{fieldName} must be a valid GUID."));
    }

    private static InvoiceResponse MapToResponse(Domain.Aggregates.Invoice invoice) =>
        new()
        {
            Id = invoice.Id.Value.ToString(),
            PatientId = invoice.PatientId.ToString(),
            EncounterId = invoice.EncounterId?.ToString() ?? string.Empty,
            InvoiceNumber = invoice.InvoiceNumber,
            InvoiceDate = invoice.InvoiceDate.ToTimestamp(),
            DueDate = invoice.DueDate?.ToTimestamp(),
            StatusCode = invoice.Status.Code,
            StatusName = invoice.Status.Name,
            Notes = invoice.Notes ?? string.Empty,
            SubTotal = (double)invoice.SubTotal,
            TaxAmount = (double)invoice.TaxAmount,
            DiscountAmount = (double)invoice.DiscountAmount,
            TotalAmount = (double)invoice.TotalAmount,
            PaidAmount = (double)invoice.PaidAmount,
            BalanceDue = (double)invoice.BalanceDue,
            CreatedAt = invoice.CreatedAt.ToTimestamp(),
            UpdatedAt = invoice.UpdatedAt?.ToTimestamp()
        };
}
