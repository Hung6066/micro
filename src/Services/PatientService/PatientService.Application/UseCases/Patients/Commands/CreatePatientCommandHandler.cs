using AutoMapper;
using His.Hope.PatientService.Domain.Aggregates;
using His.Hope.PatientService.Domain.Repositories;
using His.Hope.PatientService.Domain.ValueObjects;
using His.Hope.PatientService.Application.DTOs;
using His.Hope.SharedKernel.Domain.Common;
using His.Hope.SharedKernel.Domain.ValueObjects;
using MediatR;

namespace His.Hope.PatientService.Application.UseCases.Patients.Commands;

public class CreatePatientCommandHandler : IRequestHandler<CreatePatientCommand, PatientDto>
{
    private readonly IPatientRepository _patientRepository;
    private readonly IMapper _mapper;
    private readonly DomainEventDispatcher _eventDispatcher;
    private readonly IUnitOfWork _unitOfWork;

    public CreatePatientCommandHandler(
        IPatientRepository patientRepository,
        IMapper mapper,
        DomainEventDispatcher eventDispatcher)
    {
        _patientRepository = patientRepository;
        _mapper = mapper;
        _eventDispatcher = eventDispatcher;
        _unitOfWork = patientRepository.UnitOfWork;
    }

    public async Task<PatientDto> Handle(CreatePatientCommand request,
        CancellationToken cancellationToken)
    {
        var name = new PersonName(request.FirstName, request.LastName, request.MiddleName);
        var gender = Gender.FromCode(request.GenderCode);
        var contactInfo = new ContactInfo(request.Phone, request.Email);
        var address = new Address(
            request.Street,
            string.IsNullOrWhiteSpace(request.District) ? "-" : request.District!,
            request.City, request.Province,
            request.PostalCode ?? string.Empty, request.Country);

        var patient = Patient.Register(name, request.DateOfBirth, gender, contactInfo, address);

        if (!string.IsNullOrEmpty(request.InsuranceId))
            patient.UpdateInsurance(request.InsuranceId);

        if (!string.IsNullOrEmpty(request.NationalId))
            patient.UpdateNationalId(request.NationalId);

        await _patientRepository.AddAsync(patient, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<PatientDto>(patient);
    }
}
