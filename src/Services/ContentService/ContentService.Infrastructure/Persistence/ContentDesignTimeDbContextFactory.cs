using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace His.Hope.ContentService.Infrastructure;

public sealed class ContentDesignTimeDbContextFactory : IDesignTimeDbContextFactory<ContentDbContext>
{
    public ContentDbContext CreateDbContext(string[] args)
    {
        var connection = ReadArgument(args, "--connection")
            ?? Environment.GetEnvironmentVariable("DATABASE_CONTENT_URL")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__ContentDb");

        if (string.IsNullOrWhiteSpace(connection))
        {
            throw new InvalidOperationException(
                "ContentDb connection is required. Pass --connection or set DATABASE_CONTENT_URL.");
        }

        var options = new DbContextOptionsBuilder<ContentDbContext>()
            .UseNpgsql(connection, npgsql =>
                npgsql.MigrationsAssembly(typeof(ContentDbContext).Assembly.GetName().Name))
            .Options;

        return new ContentDbContext(options);
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
