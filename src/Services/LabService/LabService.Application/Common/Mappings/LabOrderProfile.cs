using AutoMapper;
using His.Hope.LabService.Domain.Aggregates;
using His.Hope.LabService.Domain.Entities;
using His.Hope.LabService.Application.DTOs;

namespace His.Hope.LabService.Application.Common.Mappings;

public class LabOrderProfile : Profile
{
    public LabOrderProfile()
    {
        CreateMap<LabOrder, LabOrderDto>()
            .ForMember(d => d.Id, o => o.MapFrom(s => s.Id.Value))
            .ForMember(d => d.StatusCode, o => o.MapFrom(s => s.Status.Code))
            .ForMember(d => d.StatusName, o => o.MapFrom(s => s.Status.Name))
            .ForMember(d => d.PriorityCode, o => o.MapFrom(s => s.Priority.Code))
            .ForMember(d => d.PriorityName, o => o.MapFrom(s => s.Priority.Name));

        CreateMap<LabTest, LabTestDto>()
            .ForMember(d => d.Id, o => o.MapFrom(s => s.Id.Value))
            .ForMember(d => d.LabOrderId, o => o.MapFrom(s => s.LabOrderId.Value))
            .ForMember(d => d.StatusCode, o => o.MapFrom(s => s.Status.Code))
            .ForMember(d => d.StatusName, o => o.MapFrom(s => s.Status.Name));

        CreateMap<LabResult, LabResultDto>()
            .ForMember(d => d.LabResultId, o => o.MapFrom(s => s.LabResultId.Value))
            .ForMember(d => d.AbnormalFlagCode, o => o.MapFrom(s => s.AbnormalFlag != null ? s.AbnormalFlag.Code : null))
            .ForMember(d => d.AbnormalFlagName, o => o.MapFrom(s => s.AbnormalFlag != null ? s.AbnormalFlag.Name : null))
            .ForMember(d => d.ResultStatusCode, o => o.MapFrom(s => s.ResultStatus.Code))
            .ForMember(d => d.ResultStatusName, o => o.MapFrom(s => s.ResultStatus.Name));
    }
}
