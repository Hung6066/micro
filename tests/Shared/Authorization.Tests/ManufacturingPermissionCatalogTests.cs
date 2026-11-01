using FluentAssertions;
using His.Hope.SharedKernel.Authorization;
using Xunit;

namespace His.Hope.Authorization.Tests;

public sealed class ManufacturingPermissionCatalogTests
{
    [Fact]
    public void Field_operation_permissions_are_registered_with_descriptors()
    {
        HisHopePermissions.Manufacturing.ProductionExecute.Should().Be("manufacturing.production.execute");
        HisHopePermissions.Manufacturing.QualityInspect.Should().Be("manufacturing.quality.inspect");
        HisHopePermissions.Manufacturing.MaintenanceComplete.Should().Be("manufacturing.maintenance.complete");

        HisHopePermissions.All.Should().Contain(HisHopePermissions.Manufacturing.ProductionExecute);
        HisHopePermissions.AllDescriptors.Select(descriptor => descriptor.Code)
            .Should().Contain(HisHopePermissions.Manufacturing.QualityInspect);
    }
}
