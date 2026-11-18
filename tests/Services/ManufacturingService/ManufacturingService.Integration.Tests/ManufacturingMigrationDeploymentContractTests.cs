using System.Text.RegularExpressions;
using Xunit;

namespace ManufacturingService.Integration.Tests;

public sealed class ManufacturingMigrationDeploymentContractTests
{
    [Fact]
    public void ProductionManufacturingRunsMigrationsAtServiceStartupWithoutOpenShipHook()
    {
        var repositoryRoot = FindRepositoryRoot();
        var compose = File.ReadAllText(Path.Combine(repositoryRoot, "docker", "docker-compose.openship.yml"));
        var service = Regex.Match(
            compose,
            @"(?ms)^  manufacturingservice:\r?\n(?<body>.*?)(?=^  [A-Za-z0-9_-]+:\s*$|\z)");
        Assert.True(service.Success, "Compose must declare manufacturingservice.");
        Assert.Matches(
            @"(?m)^\s+Persistence__RunMigrationsOnStartup:\s*['""]?true['""]?\s*$",
            service.Groups["body"].Value);

        var openShip = File.ReadAllText(Path.Combine(repositoryRoot, "openship.yaml"));
        var preDeploy = Regex.Match(
            openShip,
            @"(?ms)^preDeploy:\r?\n(?<body>.*?)(?=^[A-Za-z][A-Za-z0-9_-]*:\s*$|\z)");
        Assert.True(preDeploy.Success, "OpenShip must declare its preDeploy configuration.");
        Assert.DoesNotMatch(@"(?m)^\s+service:\s*manufacturingservice\s*$", preDeploy.Groups["body"].Value);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "openship.yaml"))
                && Directory.Exists(Path.Combine(directory.FullName, "src", "Services", "ManufacturingService")))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException("Could not find the Micro repository root from the test output directory.");
    }
}
