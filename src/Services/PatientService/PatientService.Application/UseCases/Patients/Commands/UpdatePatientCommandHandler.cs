using AutoMapper;
using His.Hope.PatientService.Domain.Aggregates;
using His.Hope.PatientService.Domain.Entities;
using His.Hope.PatientService.Domain.Repositories;
using His.Hope.PatientService.Domain.ValueObjects;
using His.Hope.PatientService.Application.DTOs;
using His.Hope.SharedKernel.Domain.Common;
using His.Hope.SharedKernel.Domain.ValueObjects;
using MediatR;

namespace His.Hope.PatientService.Application.UseCases.Patients.Commands;

public class UpdatePatientCommandHandler : IRequestHandler<UpdatePatientCommand, PatientDto>
{
    private readonly IPatientRepository _patientRepository;
    private readonly IMapper _mapper;
    private readonly IUnitOfWork _unitOfWork;

    public UpdatePatientCommandHandler(
        IPatientRepository patientRepository,
        IMapper mapper)
    {
        _patientRepository = patientRepository;
        _mapper = mapper;
        _unitOfWork = patientRepository.UnitOfWork;
    }

    public async Task<PatientDto> Handle(UpdatePatientCommand request,
        CancellationToken cancellationToken)
    {
        var patientId = PatientId.From(request.Id);
        var patient = await _patientRepository.GetByIdAsync(patientId, cancellationToken);

        if (patient is null)
            throw new NotFoundException(nameof(Patient), request.Id);

        var name = new PersonName(request.FirstName, request.LastName, request.MiddleName);
        var gender = request.GenderCode is not null ? Gender.FromCode(request.GenderCode) : null;
        var contactInfo = new ContactInfo(request.Phone, request.Email);
        var address = new Address(
            request.Street,
            string.IsNullOrWhiteSpace(request.District) ? "-" : request.District!,
            request.City, request.Province,
            request.PostalCode ?? string.Empty, request.Country);

        patient.UpdatePersonalInfo(name, request.DateOfBirth, gender, contactInfo, address);

        await _patientRepository.UpdateAsync(patient, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<PatientDto>(patient);
    }
}
