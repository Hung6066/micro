using AutoMapper;
using His.Hope.BillingService.Domain.Aggregates;
using His.Hope.BillingService.Domain.Entities;
using His.Hope.BillingService.Domain.Repositories;
using His.Hope.BillingService.Domain.ValueObjects;
using His.Hope.BillingService.Application.Common.Exceptions;
using His.Hope.BillingService.Application.DTOs;
using His.Hope.SharedKernel.Domain.Common;
using MediatR;

namespace His.Hope.BillingService.Application.UseCases.Invoices.Commands;

public class RecordPaymentCommandHandler : IRequestHandler<RecordPaymentCommand, InvoiceDto>
{
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IMapper _mapper;
    private readonly IUnitOfWork _unitOfWork;

    public RecordPaymentCommandHandler(
        IInvoiceRepository invoiceRepository,
        IMapper mapper)
    {
        _invoiceRepository = invoiceRepository;
        _mapper = mapper;
        _unitOfWork = invoiceRepository.UnitOfWork;
    }

    public async Task<InvoiceDto> Handle(RecordPaymentCommand request,
        CancellationToken cancellationToken)
    {
        var invoiceId = InvoiceId.From(request.InvoiceId);
        var invoice = await _invoiceRepository.GetByIdAsync(invoiceId, cancellationToken);

        if (invoice is null)
            throw new NotFoundException(nameof(Invoice), request.InvoiceId);

        var method = PaymentMethod.FromCode(request.MethodCode);

        var payment = Payment.Create(
            invoiceId,
            request.PatientId,
            request.Amount,
            request.PaymentDate,
            method,
            request.ReferenceNumber,
            request.Notes);

        payment.MarkCompleted();
        invoice.RecordPayment(payment);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<InvoiceDto>(invoice);
    }
}
