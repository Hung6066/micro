using AutoMapper;
using His.Hope.PharmacyService.Domain.Aggregates;
using His.Hope.PharmacyService.Application.DTOs;

namespace His.Hope.PharmacyService.Application.Common.Mappings;

public class PharmacyProfile : Profile
{
    public PharmacyProfile()
    {
        CreateMap<Medication, MedicationDto>()
            .ForMember(d => d.Id, o => o.MapFrom(s => s.Id.Value));

        CreateMap<Prescription, PrescriptionDto>()
            .ForMember(d => d.Id, o => o.MapFrom(s => s.Id.Value))
            .ForMember(d => d.StatusCode, o => o.MapFrom(s => s.Status.Code))
            .ForMember(d => d.StatusName, o => o.MapFrom(s => s.Status.Name))
            .ForMember(d => d.Medications, o => o.MapFrom(s => new List<PrescriptionMedicationDto>
            {
                new()
                {
                    MedicationId = s.MedicationId,
                    MedicationName = s.MedicationName,
                    Strength = s.Strength,
                    DosageForm = s.DosageForm,
                    DosageInstructions = s.DosageInstructions,
                    Route = s.Route,
                    Quantity = s.Quantity,
                    Refills = s.Refills
                }
            }));
    }
}
