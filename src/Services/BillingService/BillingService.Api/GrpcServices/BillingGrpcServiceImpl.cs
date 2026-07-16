using AutoMapper;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using His.Hope.BillingGrpc;
using His.Hope.BillingService.Domain.Repositories;
using His.Hope.BillingService.Domain.ValueObjects;
using His.Hope.BillingService.Application.DTOs;
using His.Hope.BillingService.Application.UseCases.Invoices.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;

namespace His.Hope.BillingService.Api.GrpcServices;

[Authorize]
public class BillingGrpcServiceImpl : BillingGrpcService.BillingGrpcServiceBase
{
    private readonly IMediator _mediator;
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IMapper _mapper;

    public BillingGrpcServiceImpl(
        IMediator mediator,
        IInvoiceRepository invoiceRepository,
        IMapper mapper)
    {
        _mediator = mediator;
        _invoiceRepository = invoiceRepository;
        _mapper = mapper;
    }

    public override async Task<InvoiceResponse> GetInvoice(InvoiceRequest request,
        ServerCallContext context)
    {
        var invoiceId = InvoiceId.From(Guid.Parse(request.Id));
        var invoice = await _invoiceRepository.GetByIdAsync(invoiceId);

        if (invoice is null)
            throw new RpcException(new Status(StatusCode.NotFound, "Invoice not found"));

        return MapToResponse(invoice);
    }

    public override async Task<InvoiceResponse> GetInvoiceByNumber(
        InvoiceByNumberRequest request, ServerCallContext context)
    {
        var invoice = await _invoiceRepository.GetByInvoiceNumberAsync(request.InvoiceNumber);

        if (invoice is null)
            throw new RpcException(new Status(StatusCode.NotFound, "Invoice not found"));

        return MapToResponse(invoice);
    }

    public override async Task<InvoiceListResponse> GetPatientInvoices(
        PatientInvoicesRequest request, ServerCallContext context)
    {
        var patientId = Guid.Parse(request.PatientId);
        var page = request.Page > 0 ? request.Page : 1;
        var pageSize = request.PageSize > 0 ? request.PageSize : 20;

        var allInvoices = await _invoiceRepository.GetByPatientAsync(patientId);
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
        var invoiceId = InvoiceId.From(Guid.Parse(request.Id));
        var invoice = await _invoiceRepository.GetByIdAsync(invoiceId);

        return new InvoiceExistsResponse { Exists = invoice is not null };
    }

    public override async Task<InvoiceListResponse> SearchInvoices(
        InvoiceSearchRequest request, ServerCallContext context)
    {
        var page = request.Page > 0 ? request.Page : 1;
        var pageSize = request.PageSize > 0 ? request.PageSize : 20;
        var result = await _invoiceRepository.SearchAsync(
            request.SearchTerm, (int)page, (int)pageSize);

        var response = new InvoiceListResponse();
        response.Invoices.AddRange(result.Items.Select(MapToResponse));
        response.TotalCount = result.TotalCount;
        response.Page = page;
        response.PageSize = pageSize;
        return response;
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
