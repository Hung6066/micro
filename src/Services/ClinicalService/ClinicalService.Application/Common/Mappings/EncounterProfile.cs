using AutoMapper;
using His.Hope.ClinicalService.Domain.Aggregates;
using His.Hope.ClinicalService.Domain.ValueObjects;
using His.Hope.ClinicalService.Application.DTOs;

namespace His.Hope.ClinicalService.Application.Common.Mappings;

public class EncounterProfile : Profile
{
    public EncounterProfile()
    {
        CreateMap<Encounter, EncounterDto>()
            .ForMember(d => d.Id, o => o.MapFrom(s => s.Id.Value))
            .ForMember(d => d.EncounterTypeCode, o => o.MapFrom(s => s.EncounterType.Code))
            .ForMember(d => d.EncounterTypeName, o => o.MapFrom(s => s.EncounterType.Name))
            .ForMember(d => d.StatusCode, o => o.MapFrom(s => s.Status.Code))
            .ForMember(d => d.StatusName, o => o.MapFrom(s => s.Status.Name))
            .ForMember(d => d.Hpi, o => o.MapFrom(s => s.Hpi))
            .ForMember(d => d.VitalSigns, o => o.MapFrom(s => s.VitalSigns))
            .ForMember(d => d.Diagnoses, o => o.MapFrom(s => s.Diagnoses))
            .ForMember(d => d.Procedures, o => o.MapFrom(s => s.Procedures));

        CreateMap<HistoryPresentIllness, HpiDto>();
        CreateMap<VitalSigns, VitalSignsDto>();
        CreateMap<Diagnosis, DiagnosisDto>();
        CreateMap<Procedure, ProcedureDto>();
    }
}
