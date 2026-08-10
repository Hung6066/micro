using FluentAssertions;
using His.Hope.IdentityService.Domain.Entities;

namespace His.Hope.IdentityService.Domain.Tests;

public class SystemSettingTests
{
    [Fact]
    public void Create_WithRequiredFields_ShouldSetProperties()
    {
        var setting = new SystemSetting
        {
            Key = "hospital.name",
            Value = "His.Hope General Hospital"
        };

        setting.Key.Should().Be("hospital.name");
        setting.Value.Should().Be("His.Hope General Hospital");
        setting.Description.Should().BeNull();
        setting.Category.Should().BeNull();
        setting.UpdatedBy.Should().BeNull();
        setting.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_WithAllFields_ShouldSetCorrectly()
    {
        var setting = new SystemSetting
        {
            Key = "system.sessionTimeout",
            Value = "30",
            Description = "Session timeout in minutes",
            Category = "system",
            UpdatedBy = "admin-1",
            UpdatedAt = new DateTime(2024, 6, 15, 8, 0, 0, DateTimeKind.Utc)
        };

        setting.Description.Should().Be("Session timeout in minutes");
        setting.Category.Should().Be("system");
        setting.UpdatedBy.Should().Be("admin-1");
        setting.UpdatedAt.Should().Be(new DateTime(2024, 6, 15, 8, 0, 0, DateTimeKind.Utc));
    }

    [Theory]
    [InlineData("hospital.name", "His.Hope General Hospital")]
    [InlineData("hospital.address", "123 Healthcare Ave")]
    [InlineData("system.sessionTimeout", "30")]
    [InlineData("system.mfaRequired", "true")]
    [InlineData("clinical.defaultEncounterType", "OP")]
    [InlineData("billing.currency", "VND")]
    public void WithVariousSettings_ShouldStoreCorrectly(string key, string value)
    {
        var setting = new SystemSetting { Key = key, Value = value };

        setting.Key.Should().Be(key);
        setting.Value.Should().Be(value);
    }

    [Fact]
    public void UpdatedAt_DefaultsToUtcNow()
    {
        var setting = new SystemSetting { Key = "test.key", Value = "test" };

        setting.UpdatedAt.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void UpdateValue_ShouldReflectChange()
    {
        var setting = new SystemSetting
        {
            Key = "hospital.name",
            Value = "Old Name"
        };

        setting.Value = "New Name";
        setting.UpdatedAt = DateTime.UtcNow;

        setting.Value.Should().Be("New Name");
        setting.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void WithCategories_ShouldGroupCorrectly()
    {
        var hospital = new SystemSetting { Key = "hospital.name", Value = "H", Category = "hospital" };
        var system = new SystemSetting { Key = "system.timeout", Value = "30", Category = "system" };
        var clinical = new SystemSetting { Key = "clinical.defaultType", Value = "OP", Category = "clinical" };

        hospital.Category.Should().Be("hospital");
        system.Category.Should().Be("system");
        clinical.Category.Should().Be("clinical");
    }
}
