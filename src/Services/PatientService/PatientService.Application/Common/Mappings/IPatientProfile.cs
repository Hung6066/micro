using AutoMapper;
using His.Hope.PatientService.Domain.Aggregates;
using His.Hope.PatientService.Application.DTOs;

namespace His.Hope.PatientService.Application.Common.Mappings;

public class PatientProfile : Profile
{
    public PatientProfile()
    {
        CreateMap<Patient, PatientDto>()
            .ForMember(d => d.Id, o => o.MapFrom(s => s.Id.Value))
            .ForMember(d => d.FullName, o => o.MapFrom(s => s.Name.FullName))
            .ForMember(d => d.FirstName, o => o.MapFrom(s => s.Name.FirstName))
            .ForMember(d => d.LastName, o => o.MapFrom(s => s.Name.LastName))
            .ForMember(d => d.MiddleName, o => o.MapFrom(s => s.Name.MiddleName))
            .ForMember(d => d.Phone, o => o.MapFrom(s => s.ContactInfo.Phone))
            .ForMember(d => d.Email, o => o.MapFrom(s => s.ContactInfo.Email))
            .ForMember(d => d.Street, o => o.MapFrom(s => s.Address.Street))
            .ForMember(d => d.District, o => o.MapFrom(s => s.Address.District))
            .ForMember(d => d.City, o => o.MapFrom(s => s.Address.City))
            .ForMember(d => d.Province, o => o.MapFrom(s => s.Address.Province))
            .ForMember(d => d.PostalCode, o => o.MapFrom(s => s.Address.PostalCode))
            .ForMember(d => d.Country, o => o.MapFrom(s => s.Address.Country))
            .ForMember(d => d.GenderCode, o => o.MapFrom(s => s.Gender.Code))
            .ForMember(d => d.GenderName, o => o.MapFrom(s => s.Gender.Name))
            .ForMember(d => d.BloodTypeCode, o => o.MapFrom(s => s.BloodType != null ? s.BloodType.Code : null))
            .ForMember(d => d.BloodTypeName, o => o.MapFrom(s => s.BloodType != null ? s.BloodType.Name : null))
            .ForMember(d => d.RaceCode, o => o.MapFrom(s => s.Race != null ? s.Race.Code : null))
            .ForMember(d => d.MaritalStatusCode, o => o.MapFrom(s => s.MaritalStatus != null ? s.MaritalStatus.Code : null))
            .ForMember(d => d.Allergies, o => o.MapFrom(s => s.Allergies))
            .ForMember(d => d.Conditions, o => o.MapFrom(s => s.Conditions));

        CreateMap<PatientService.Domain.Entities.Allergy, AllergyDto>();
        CreateMap<PatientService.Domain.Entities.MedicalCondition, ConditionDto>();
    }
}
