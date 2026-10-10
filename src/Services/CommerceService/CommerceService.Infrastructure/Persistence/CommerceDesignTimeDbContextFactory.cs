using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace His.Hope.CommerceService.Infrastructure.Persistence;

public sealed class CommerceDesignTimeDbContextFactory : IDesignTimeDbContextFactory<CommerceDbContext>
{
    public CommerceDbContext CreateDbContext(string[] args)
    {
        var connection = ReadArgument(args, "--connection")
            ?? Environment.GetEnvironmentVariable("DATABASE_COMMERCE_URL")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__CommerceDb")
            ?? "Host=localhost;Port=5432;Database=commerce_design;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<CommerceDbContext>()
            .UseNpgsql(connection, npgsql =>
                npgsql.MigrationsAssembly(typeof(CommerceDbContext).Assembly.GetName().Name))
            .Options;

        return new CommerceDbContext(options);
    }

    private static string? ReadArgument(string[] args, string name)
    {
        for (var index = 0; index < args.Length - 1; index++)
        {
            if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase))
                return args[index + 1];
        }

        return null;
    }
}
