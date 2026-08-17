using AutoMapper;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using His.Hope.LabGrpc;
using His.Hope.LabService.Domain.Repositories;
using His.Hope.LabService.Domain.Entities;
using His.Hope.LabService.Infrastructure.Persistence;
using His.Hope.Authorization;
using His.Hope.SharedKernel.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace His.Hope.LabService.Api.GrpcServices;

[Authorize(Policy = AuthorizationPolicyNames.Permissions.LabView)]
public class LabGrpcServiceImpl : LabGrpcService.LabGrpcServiceBase
{
    private readonly ILabOrderRepository _labOrderRepository;
    private readonly IMapper _mapper;
    private readonly LabDbContext _db;
    private readonly IResourceAuthorizationEvaluator _authorization;

    public LabGrpcServiceImpl(
        ILabOrderRepository labOrderRepository,
        IMapper mapper,
        LabDbContext db,
        IResourceAuthorizationEvaluator authorization)
    {
        _labOrderRepository = labOrderRepository;
        _mapper = mapper;
        _db = db;
        _authorization = authorization;
    }

    public override async Task<LabOrderResponse> GetLabOrder(LabOrderRequest request,
        ServerCallContext context)
    {
        var labOrderId = LabOrderId.From(ParseGuidOrThrow(request.Id, "Lab order id"));
        await EnsureResourceAccessAsync(labOrderId, context);
        var labOrder = await _labOrderRepository.GetByIdAsync(labOrderId);

        if (labOrder is null)
            throw new RpcException(new Status(StatusCode.NotFound, "Lab order not found"));

        return MapToResponse(labOrder);
    }

    public override async Task<LabOrderListResponse> GetPatientLabOrders(
        PatientLabOrdersRequest request, ServerCallContext context)
    {
        var patientId = ParseGuidOrThrow(request.PatientId, "Patient id");
        var page = request.Page > 0 ? request.Page : 1;
        var pageSize = request.PageSize > 0 ? request.PageSize : 20;
        var accessScope = FacilityAccessScope.FromPrincipal(context.GetHttpContext().User);

        var allLabOrders = await _labOrderRepository.GetByPatientAsync(patientId,
            accessScope.FacilityIds, accessScope.IsCrossFacility, context.CancellationToken);
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
        var labOrderId = LabOrderId.From(ParseGuidOrThrow(request.Id, "Lab order id"));
        await EnsureResourceAccessAsync(labOrderId, context);
        var labOrder = await _labOrderRepository.GetByIdAsync(labOrderId);
        return new LabOrderExistsResponse { Exists = labOrder is not null };
    }

    private async Task EnsureResourceAccessAsync(LabOrderId labOrderId, ServerCallContext context)
    {
        var decision = await _authorization.EvaluateResourceAsync(
            _db.LabOrders,
            order => order.Id == labOrderId,
            order => order.FacilityId,
            context.GetHttpContext().User,
            HisHopePermissions.LabOrders.View,
            "lab-order",
            labOrderId.Value.ToString("D"),
            context.CancellationToken);
        if (!decision.Allowed)
            throw new RpcException(new Status(StatusCode.NotFound, "Lab order not found"));
    }

    public override async Task<LabOrderListResponse> SearchLabOrders(
        LabOrderSearchRequest request, ServerCallContext context)
    {
        var page = request.Page > 0 ? request.Page : 1;
        var pageSize = request.PageSize > 0 ? request.PageSize : 20;
        var accessScope = FacilityAccessScope.FromPrincipal(context.GetHttpContext().User);
        var (items, totalCount) = await _labOrderRepository.SearchAsync(
            request.SearchTerm, page, pageSize, null, null, null, null,
            accessScope.FacilityIds, accessScope.IsCrossFacility, context.CancellationToken);

        var response = new LabOrderListResponse();
        response.LabOrders.AddRange(items.Select(MapToResponse));
        response.TotalCount = totalCount;
        response.Page = page;
        response.PageSize = pageSize;
        return response;
    }

    private static Guid ParseGuidOrThrow(string value, string fieldName)
    {
        if (Guid.TryParse(value, out var parsed))
            return parsed;

        throw new RpcException(new Status(StatusCode.InvalidArgument, $"{fieldName} must be a valid GUID."));
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
