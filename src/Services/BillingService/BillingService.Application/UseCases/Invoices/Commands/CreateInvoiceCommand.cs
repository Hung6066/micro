using His.Hope.BillingService.Application.DTOs;
using MediatR;

namespace His.Hope.BillingService.Application.UseCases.Invoices.Commands;

public record LineItemInput(
    string Description,
    int Quantity,
    decimal UnitPrice,
    string? ItemCode,
    string? ItemTypeCode);

public record CreateInvoiceCommand(
    Guid PatientId,
    Guid? EncounterId,
    DateTime InvoiceDate,
    DateTime? DueDate,
    string InvoiceNumber,
    string? Notes,
    ICollection<LineItemInput> LineItems) : IRequest<InvoiceDto>;
