using AutoMapper;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using His.Hope.LabGrpc;
using His.Hope.LabService.Domain.Repositories;
using His.Hope.LabService.Domain.Entities;
using Microsoft.AspNetCore.Authorization;

namespace His.Hope.LabService.Api.GrpcServices;

[Authorize]
public class LabGrpcServiceImpl : LabGrpcService.LabGrpcServiceBase
{
    private readonly ILabOrderRepository _labOrderRepository;
    private readonly IMapper _mapper;

    public LabGrpcServiceImpl(
        ILabOrderRepository labOrderRepository,
        IMapper mapper)
    {
        _labOrderRepository = labOrderRepository;
        _mapper = mapper;
    }

    public override async Task<LabOrderResponse> GetLabOrder(LabOrderRequest request,
        ServerCallContext context)
    {
        var labOrderId = LabOrderId.From(Guid.Parse(request.Id));
        var labOrder = await _labOrderRepository.GetByIdAsync(labOrderId);

        if (labOrder is null)
            throw new RpcException(new Status(StatusCode.NotFound, "Lab order not found"));

        return MapToResponse(labOrder);
    }

    public override async Task<LabOrderListResponse> GetPatientLabOrders(
        PatientLabOrdersRequest request, ServerCallContext context)
    {
        var patientId = Guid.Parse(request.PatientId);
        var page = request.Page > 0 ? request.Page : 1;
        var pageSize = request.PageSize > 0 ? request.PageSize : 20;

        var allLabOrders = await _labOrderRepository.GetByPatientAsync(patientId);
        var totalCount = allLabOrders.Count;
        var paged = allLabOrders
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(MapToResponse);

        var response = new LabOrderListResponse();
        response.LabOrders.AddRange(paged);
        response.TotalCount = totalCount;
        response.Page = page;
        response.PageSize = pageSize;
        return response;
    }

    public override async Task<LabOrderExistsResponse> CheckLabOrderExists(
        LabOrderExistsRequest request, ServerCallContext context)
    {
        var labOrderId = LabOrderId.From(Guid.Parse(request.Id));
        var labOrder = await _labOrderRepository.GetByIdAsync(labOrderId);
        return new LabOrderExistsResponse { Exists = labOrder is not null };
    }

    public override async Task<LabOrderListResponse> SearchLabOrders(
        LabOrderSearchRequest request, ServerCallContext context)
    {
        var page = request.Page > 0 ? request.Page : 1;
        var pageSize = request.PageSize > 0 ? request.PageSize : 20;
        var (items, totalCount) = await _labOrderRepository.SearchAsync(
            request.SearchTerm, page, pageSize);

        var response = new LabOrderListResponse();
        response.LabOrders.AddRange(items.Select(MapToResponse));
        response.TotalCount = totalCount;
        response.Page = page;
        response.PageSize = pageSize;
        return response;
    }

    private static LabOrderResponse MapToResponse(Domain.Aggregates.LabOrder labOrder)
    {
        var response = new LabOrderResponse
        {
            Id = labOrder.Id.Value.ToString(),
            PatientId = labOrder.PatientId.ToString(),
            ProviderId = labOrder.ProviderId.ToString(),
            EncounterId = labOrder.EncounterId?.ToString() ?? string.Empty,
            OrderDate = labOrder.OrderDate.ToTimestamp(),
            StatusCode = labOrder.Status.Code,
            StatusName = labOrder.Status.Name,
            PriorityCode = labOrder.Priority.Code,
            PriorityName = labOrder.Priority.Name,
            Notes = labOrder.Notes ?? string.Empty,
        };

        response.Tests.AddRange(labOrder.RequestedTests.Select(MapTestToResponse));
        return response;
    }

    private static LabTestResponse MapTestToResponse(Domain.Entities.LabTest test)
    {
        var response = new LabTestResponse
        {
            Id = test.Id.Value.ToString(),
            TestCode = test.TestCode,
            TestName = test.TestName,
            SpecimenType = test.SpecimenType ?? string.Empty,
            StatusCode = test.Status.Code,
            StatusName = test.Status.Name,
            OrderedAt = test.OrderedAt.ToTimestamp(),
            CollectedAt = test.CollectedAt?.ToTimestamp(),
            CompletedAt = test.CompletedAt?.ToTimestamp(),
        };

        if (test.Result != null)
        {
            response.Result = new LabResultResponse
            {
                LabResultId = test.Result.LabResultId.Value.ToString(),
                Value = test.Result.Value,
                Unit = test.Result.Unit ?? string.Empty,
                ReferenceRange = test.Result.ReferenceRange ?? string.Empty,
                AbnormalFlagCode = test.Result.AbnormalFlag?.Code ?? string.Empty,
                AbnormalFlagName = test.Result.AbnormalFlag?.Name ?? string.Empty,
                ResultStatusCode = test.Result.ResultStatus.Code,
                ResultStatusName = test.Result.ResultStatus.Name,
                ResultedAt = test.Result.ResultedAt.ToTimestamp(),
                PerformedBy = test.Result.PerformedBy ?? string.Empty,
                Notes = test.Result.Notes ?? string.Empty,
            };
        }

        return response;
    }
}
