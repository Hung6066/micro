using AutoMapper;
using His.Hope.BillingService.Domain.Aggregates;
using His.Hope.BillingService.Domain.Entities;
using His.Hope.BillingService.Application.DTOs;

namespace His.Hope.BillingService.Application.Common.Mappings;

public class InvoiceProfile : Profile
{
    public InvoiceProfile()
    {
        CreateMap<Invoice, InvoiceDto>()
            .ForMember(d => d.Id, o => o.MapFrom(s => s.Id.Value))
            .ForMember(d => d.StatusCode, o => o.MapFrom(s => s.Status.Code))
            .ForMember(d => d.StatusName, o => o.MapFrom(s => s.Status.Name));

        CreateMap<InvoiceLineItem, InvoiceLineItemDto>()
            .ForMember(d => d.Id, o => o.MapFrom(s => s.Id.Value))
            .ForMember(d => d.ItemTypeCode, o => o.MapFrom(s => s.ItemType != null ? s.ItemType.Code : null))
            .ForMember(d => d.ItemTypeName, o => o.MapFrom(s => s.ItemType != null ? s.ItemType.Name : null));

        CreateMap<Payment, PaymentDto>()
            .ForMember(d => d.Id, o => o.MapFrom(s => s.Id.Value))
            .ForMember(d => d.MethodCode, o => o.MapFrom(s => s.Method.Code))
            .ForMember(d => d.MethodName, o => o.MapFrom(s => s.Method.Name))
            .ForMember(d => d.StatusCode, o => o.MapFrom(s => s.Status.Code))
            .ForMember(d => d.StatusName, o => o.MapFrom(s => s.Status.Name));
    }
}
