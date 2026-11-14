using FluentAssertions;
using His.Hope.AspNetCore.Tenancy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace His.Hope.AspNetCore.Tests;

public sealed class TenantPlacementRegistryTests
{
    [Fact]
    public void ResolveConnectionName_uses_default_when_routing_disabled()
    {
        var registry = CreateRegistry(new TenantPlacementOptions
        {
            Enabled = false,
            Services =
            {
                ["manufacturing"] = new TenantPlacementServiceOptions
                {
                    DefaultConnectionName = "ManufacturingDb"
                }
            },
            Placements =
            {
                new TenantPlacementEntryOptions
                {
                    TenantKey = "customer-enterprise-y",
                    Tier = TenantPlacementTier.Dedicated,
                    Active = true,
                    Services =
                    {
                        ["manufacturing"] = new TenantPlacementServiceBindingOptions
                        {
                            ConnectionName = "ManufacturingDb_customer_enterprise_y"
                        }
                    }
                }
            }
        });

        registry.UsesDedicatedDataStore("manufacturing", "customer-enterprise-y").Should().BeFalse();
        registry.ResolveConnectionName("manufacturing", "customer-enterprise-y").Should().Be("ManufacturingDb");
    }

    [Fact]
    public void ResolveConnectionName_uses_dedicated_binding_when_routing_enabled()
    {
        var registry = CreateRegistry(new TenantPlacementOptions
        {
            Enabled = true,
            Services =
            {
                ["manufacturing"] = new TenantPlacementServiceOptions
                {
                    DefaultConnectionName = "ManufacturingDb"
                }
            },
            Placements =
            {
                new TenantPlacementEntryOptions
                {
                    TenantKey = "customer-enterprise-y",
                    Tier = TenantPlacementTier.Dedicated,
                    Active = true,
                    Services =
                    {
                        ["manufacturing"] = new TenantPlacementServiceBindingOptions
                        {
                            ConnectionName = "ManufacturingDb_customer_enterprise_y"
                        }
                    }
                }
            }
        });

        registry.UsesDedicatedDataStore("manufacturing", "customer-enterprise-y").Should().BeTrue();
        registry.ResolveConnectionName("manufacturing", "customer-enterprise-y")
            .Should().Be("ManufacturingDb_customer_enterprise_y");
    }

    [Fact]
    public void Connection_resolver_reads_configured_connection_string()
    {
        var registry = CreateRegistry(new TenantPlacementOptions
        {
            Enabled = false,
            Services =
            {
                ["manufacturing"] = new TenantPlacementServiceOptions
                {
                    DefaultConnectionName = "ManufacturingDb"
                }
            }
        });
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ManufacturingDb"] = "Host=localhost;Database=manufacturingdb"
            })
            .Build();
        var resolver = new TenantPlacementConnectionResolver(registry, configuration);

        resolver.ResolveConnectionString("manufacturing", "customer-factory-x")
            .Should().Be("Host=localhost;Database=manufacturingdb");
    }

    [Fact]
    public void GetServiceConnectionNames_includes_default_and_dedicated_bindings_when_enabled()
    {
        var registry = CreateRegistry(new TenantPlacementOptions
        {
            Enabled = true,
            Services =
            {
                ["manufacturing"] = new TenantPlacementServiceOptions
                {
                    DefaultConnectionName = "ManufacturingDb"
                }
            },
            Placements =
            {
                new TenantPlacementEntryOptions
                {
                    TenantKey = "customer-enterprise-y",
                    Tier = TenantPlacementTier.Dedicated,
                    Active = true,
                    Services =
                    {
                        ["manufacturing"] = new TenantPlacementServiceBindingOptions
                        {
                            ConnectionName = "ManufacturingDb_customer_enterprise_y"
                        }
                    }
                },
                new TenantPlacementEntryOptions
                {
                    TenantKey = "customer-enterprise-z",
                    Tier = TenantPlacementTier.Dedicated,
                    Active = false,
                    Services =
                    {
                        ["manufacturing"] = new TenantPlacementServiceBindingOptions
                        {
                            ConnectionName = "ManufacturingDb_customer_enterprise_z"
                        }
                    }
                }
            }
        });

        registry.GetServiceConnectionNames("manufacturing")
            .Should().BeEquivalentTo(["ManufacturingDb", "ManufacturingDb_customer_enterprise_y"]);
    }

    private static TenantPlacementRegistry CreateRegistry(TenantPlacementOptions options) =>
        new(Options.Create(options), new TestHostEnvironment(), NullLogger<TenantPlacementRegistry>.Instance);

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
