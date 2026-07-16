using AutoMapper;
using His.Hope.AppointmentService.Domain.Aggregates;
using His.Hope.AppointmentService.Application.DTOs;

namespace His.Hope.AppointmentService.Application.Common.Mappings;

public class AppointmentProfile : Profile
{
    public AppointmentProfile()
    {
        CreateMap<Appointment, AppointmentDto>()
            .ForMember(d => d.Id, o => o.MapFrom(s => s.Id.Value))
            .ForMember(d => d.StatusCode, o => o.MapFrom(s => s.Status.Code))
            .ForMember(d => d.StatusName, o => o.MapFrom(s => s.Status.Name))
            .ForMember(d => d.TypeCode, o => o.MapFrom(s => s.Type.Code))
            .ForMember(d => d.TypeName, o => o.MapFrom(s => s.Type.Name));
    }
}
