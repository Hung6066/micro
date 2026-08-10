using AutoMapper;
using FluentAssertions;
using His.Hope.LabService.Application.DTOs;
using His.Hope.LabService.Application.UseCases.LabOrders.Queries;
using His.Hope.LabService.Domain.Aggregates;
using His.Hope.LabService.Domain.Entities;
using His.Hope.LabService.Domain.Repositories;
using His.Hope.LabService.Domain.ValueObjects;
using Moq;

namespace His.Hope.LabService.Application.Tests;

public class QueryHandlerTests
{
    private readonly Mock<ILabOrderRepository> _mockRepository;
    private readonly Mock<IMapper> _mockMapper;

    public QueryHandlerTests()
    {
        _mockRepository = new Mock<ILabOrderRepository>();
        _mockMapper = new Mock<IMapper>();
    }

    public class GetLabOrderByIdQueryHandlerTests : QueryHandlerTests
    {
        [Fact]
        public async Task Handle_WithExistingId_ShouldReturnDto()
        {
            var order = LabOrder.Create(Guid.NewGuid(), Guid.NewGuid(), null, LabOrderPriority.Routine, null);
            var orderId = order.Id.Value;
            _mockRepository.Setup(r => r.GetByIdAsync(It.IsAny<LabOrderId>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(order);

            var expectedDto = new LabOrderDto { Id = orderId };
            _mockMapper.Setup(m => m.Map<LabOrderDto>(order)).Returns(expectedDto);

            var handler = new GetLabOrderByIdQueryHandler(_mockRepository.Object, _mockMapper.Object);
            var query = new GetLabOrderByIdQuery(orderId);

            var result = await handler.Handle(query, CancellationToken.None);

            result.Should().NotBeNull();
            result.Should().Be(expectedDto);
        }

        [Fact]
        public async Task Handle_WithNonExistingId_ShouldReturnNull()
        {
            _mockRepository.Setup(r => r.GetByIdAsync(It.IsAny<LabOrderId>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((LabOrder?)null);

            var handler = new GetLabOrderByIdQueryHandler(_mockRepository.Object, _mockMapper.Object);
            var query = new GetLabOrderByIdQuery(Guid.NewGuid());

            var result = await handler.Handle(query, CancellationToken.None);

            result.Should().BeNull();
        }
    }

    public class GetLabOrdersByPatientQueryHandlerTests : QueryHandlerTests
    {
        [Fact]
        public async Task Handle_WithPatientId_ShouldReturnOrders()
        {
            var patientId = Guid.NewGuid();
            var orders = new List<LabOrder>
            {
                LabOrder.Create(patientId, Guid.NewGuid(), null, LabOrderPriority.Routine, null),
                LabOrder.Create(patientId, Guid.NewGuid(), null, LabOrderPriority.Urgent, null)
            };

            _mockRepository.Setup(r => r.GetByPatientAsync(patientId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(orders);

            var expectedDtos = orders.Select(o => new LabOrderDto { Id = o.Id.Value }).ToList();
            _mockMapper.Setup(m => m.Map<List<LabOrderDto>>(orders)).Returns(expectedDtos);

            var handler = new GetLabOrdersByPatientQueryHandler(_mockRepository.Object, _mockMapper.Object);
            var query = new GetLabOrdersByPatientQuery(patientId);

            var result = await handler.Handle(query, CancellationToken.None);

            result.Should().HaveCount(2);
            result.Should().BeEquivalentTo(expectedDtos);
        }

        [Fact]
        public async Task Handle_WithNoOrders_ShouldReturnEmpty()
        {
            var patientId = Guid.NewGuid();
            _mockRepository.Setup(r => r.GetByPatientAsync(patientId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<LabOrder>());

            _mockMapper.Setup(m => m.Map<List<LabOrderDto>>(It.IsAny<List<LabOrder>>()))
                .Returns(new List<LabOrderDto>());

            var handler = new GetLabOrdersByPatientQueryHandler(_mockRepository.Object, _mockMapper.Object);
            var query = new GetLabOrdersByPatientQuery(patientId);

            var result = await handler.Handle(query, CancellationToken.None);

            result.Should().BeEmpty();
        }
    }

    public class SearchLabOrdersQueryHandlerTests : QueryHandlerTests
    {
        [Fact]
        public async Task Handle_WithSearchTerm_ShouldReturnPagedResults()
        {
            var orders = new List<LabOrder>
            {
                LabOrder.Create(Guid.NewGuid(), Guid.NewGuid(), null, LabOrderPriority.Routine, null)
            };
            const int totalCount = 1;

            _mockRepository.Setup(r => r.SearchAsync(
                    It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(),
                    It.IsAny<Guid?>(), It.IsAny<string>(),
                    It.IsAny<DateTime?>(), It.IsAny<DateTime?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((orders, totalCount));

            var expectedDtos = orders.Select(o => new LabOrderDto { Id = o.Id.Value }).ToList();
            _mockMapper.Setup(m => m.Map<List<LabOrderDto>>(orders)).Returns(expectedDtos);

            var handler = new SearchLabOrdersQueryHandler(_mockRepository.Object, _mockMapper.Object);
            var query = new SearchLabOrdersQuery(Term: "CBC", Page: 1, PageSize: 20);

            var result = await handler.Handle(query, CancellationToken.None);

            result.Should().NotBeNull();
            result.Items.Should().HaveCount(1);
            result.TotalCount.Should().Be(1);
            result.Page.Should().Be(1);
            result.PageSize.Should().Be(20);
            result.HasNextPage.Should().BeFalse();
            result.HasPreviousPage.Should().BeFalse();
        }

        [Fact]
        public async Task Handle_WithFilters_ShouldPassToRepository()
        {
            var patientId = Guid.NewGuid();
            var dateFrom = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var dateTo = new DateTime(2024, 12, 31, 23, 59, 59, DateTimeKind.Utc);

            _mockRepository.Setup(r => r.SearchAsync(
                    It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(),
                    It.IsAny<Guid?>(), It.IsAny<string>(),
                    It.IsAny<DateTime?>(), It.IsAny<DateTime?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((new List<LabOrder>(), 0));

            _mockMapper.Setup(m => m.Map<List<LabOrderDto>>(It.IsAny<List<LabOrder>>()))
                .Returns(new List<LabOrderDto>());

            var handler = new SearchLabOrdersQueryHandler(_mockRepository.Object, _mockMapper.Object);
            var query = new SearchLabOrdersQuery(
                Term: "", Page: 1, PageSize: 10,
                PatientId: patientId, Status: "PENDING",
                DateFrom: dateFrom, DateTo: dateTo);

            await handler.Handle(query, CancellationToken.None);

            _mockRepository.Verify(r => r.SearchAsync(
                "", 1, 10, patientId, "PENDING", dateFrom, dateTo,
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_WithMultiplePages_ShouldSetHasNextPage()
        {
            var orders = Enumerable.Range(0, 20).Select(_ =>
                LabOrder.Create(Guid.NewGuid(), Guid.NewGuid(), null, LabOrderPriority.Routine, null)
            ).ToList();

            _mockRepository.Setup(r => r.SearchAsync(
                    It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(),
                    It.IsAny<Guid?>(), It.IsAny<string>(),
                    It.IsAny<DateTime?>(), It.IsAny<DateTime?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((orders, 50));

            _mockMapper.Setup(m => m.Map<List<LabOrderDto>>(orders))
                .Returns(orders.Select(o => new LabOrderDto { Id = o.Id.Value }).ToList());

            var handler = new SearchLabOrdersQueryHandler(_mockRepository.Object, _mockMapper.Object);
            var query = new SearchLabOrdersQuery(Page: 1, PageSize: 20);

            var result = await handler.Handle(query, CancellationToken.None);

            result.HasNextPage.Should().BeTrue();
            result.HasPreviousPage.Should().BeFalse();
        }

        [Fact]
        public async Task Handle_WithDefaultParameters_ShouldUseDefaults()
        {
            _mockRepository.Setup(r => r.SearchAsync(
                    It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(),
                    It.IsAny<Guid?>(), It.IsAny<string>(),
                    It.IsAny<DateTime?>(), It.IsAny<DateTime?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((new List<LabOrder>(), 0));

            _mockMapper.Setup(m => m.Map<List<LabOrderDto>>(It.IsAny<List<LabOrder>>()))
                .Returns(new List<LabOrderDto>());

            var handler = new SearchLabOrdersQueryHandler(_mockRepository.Object, _mockMapper.Object);
            var query = new SearchLabOrdersQuery();

            await handler.Handle(query, CancellationToken.None);

            _mockRepository.Verify(r => r.SearchAsync(
                "", 1, 20, null, null, null, null,
                It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
