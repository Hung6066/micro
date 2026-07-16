using AutoMapper;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using His.Hope.PharmacyGrpc;
using His.Hope.PharmacyService.Domain.Repositories;
using His.Hope.PharmacyService.Domain.ValueObjects;
using His.Hope.PharmacyService.Application.DTOs;
using Microsoft.AspNetCore.Authorization;

namespace His.Hope.PharmacyService.Api.GrpcServices;

[Authorize]
public class PharmacyGrpcServiceImpl : PharmacyGrpcService.PharmacyGrpcServiceBase
{
    private readonly IMedicationRepository _medicationRepository;
    private readonly IPrescriptionRepository _prescriptionRepository;
    private readonly IMapper _mapper;

    public PharmacyGrpcServiceImpl(
        IMedicationRepository medicationRepository,
        IPrescriptionRepository prescriptionRepository,
        IMapper mapper)
    {
        _medicationRepository = medicationRepository;
        _prescriptionRepository = prescriptionRepository;
        _mapper = mapper;
    }

    public override async Task<MedicationResponse> GetMedication(MedicationRequest request,
        ServerCallContext context)
    {
        var medicationId = MedicationId.From(Guid.Parse(request.Id));
        var medication = await _medicationRepository.GetByIdAsync(medicationId);

        if (medication is null)
            throw new RpcException(new Status(StatusCode.NotFound, "Medication not found"));

        return MapToResponse(medication);
    }

    public override async Task<MedicationListResponse> SearchMedications(
        MedicationSearchRequest request, ServerCallContext context)
    {
        var page = request.Page > 0 ? request.Page : 1;
        var pageSize = request.PageSize > 0 ? request.PageSize : 20;

        var (items, totalCount) = await _medicationRepository.SearchAsync(
            request.SearchTerm, page, pageSize, null, context.CancellationToken);

        var response = new MedicationListResponse();
        response.Medications.AddRange(items.Select(MapToResponse));
        response.TotalCount = totalCount;
        response.Page = page;
        response.PageSize = pageSize;
        return response;
    }

    public override async Task<MedicationExistsResponse> CheckMedicationExists(
        MedicationExistsRequest request, ServerCallContext context)
    {
        var medicationId = MedicationId.From(Guid.Parse(request.Id));
        var exists = await _medicationRepository.ExistsAsync(medicationId);

        return new MedicationExistsResponse { Exists = exists };
    }

    public override async Task<PrescriptionResponse> GetPrescription(
        PrescriptionRequest request, ServerCallContext context)
    {
        var prescriptionId = PrescriptionId.From(Guid.Parse(request.Id));
        var prescription = await _prescriptionRepository.GetByIdAsync(prescriptionId, context.CancellationToken);

        if (prescription is null)
            throw new RpcException(new Status(StatusCode.NotFound, "Prescription not found"));

        return MapPrescriptionToResponse(prescription);
    }

    public override async Task<PrescriptionListResponse> SearchPrescriptions(
        PrescriptionSearchRequest request, ServerCallContext context)
    {
        var page = request.Page > 0 ? request.Page : 1;
        var pageSize = request.PageSize > 0 ? request.PageSize : 20;

        var result = await _prescriptionRepository.SearchAsync(
            request.SearchTerm, page, pageSize, null, null, context.CancellationToken);

        var response = new PrescriptionListResponse();
        response.Prescriptions.AddRange(result.Items.Select(MapPrescriptionToResponse));
        response.TotalCount = result.TotalCount;
        response.Page = page;
        response.PageSize = pageSize;
        return response;
    }

    private static MedicationResponse MapToResponse(Domain.Aggregates.Medication medication) =>
        new()
        {
            Id = medication.Id.Value.ToString(),
            Name = medication.Name,
            GenericName = medication.GenericName ?? string.Empty,
            BrandName = medication.BrandName ?? string.Empty,
            DosageForm = medication.DosageForm,
            Strength = medication.Strength,
            Route = medication.Route ?? string.Empty,
            RequiresPrescription = medication.RequiresPrescription,
            IsActive = medication.IsActive,
            CreatedAt = medication.CreatedAt.ToTimestamp(),
            UpdatedAt = medication.UpdatedAt?.ToTimestamp()
        };

    private static PrescriptionResponse MapPrescriptionToResponse(Domain.Aggregates.Prescription prescription) =>
        new()
        {
            Id = prescription.Id.Value.ToString(),
            PatientId = prescription.PatientId.ToString(),
            ProviderId = prescription.ProviderId.ToString(),
            MedicationId = prescription.MedicationId?.ToString() ?? string.Empty,
            MedicationName = prescription.MedicationName,
            Strength = prescription.Strength,
            DosageForm = prescription.DosageForm,
            DosageInstructions = prescription.DosageInstructions,
            Route = prescription.Route ?? string.Empty,
            Quantity = prescription.Quantity,
            Refills = prescription.Refills,
            StatusCode = prescription.Status.Code,
            StatusName = prescription.Status.Name,
            PrescribedAt = prescription.PrescribedDate.ToTimestamp(),
            FilledAt = prescription.FilledDate?.ToTimestamp(),
            CreatedAt = prescription.CreatedAt.ToTimestamp(),
            UpdatedAt = prescription.UpdatedAt?.ToTimestamp()
        };
}
