using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace His.Hope.ClinicalService.Infrastructure.Persistence;

/// <summary>
/// Initializes the ClinicalService database schema on startup.
/// Called from the API layer's startup pipeline to avoid direct DbContext dependency.
/// </summary>
public static class ClinicalDbInitializer
{
    /// <summary>
    /// Applies the committed EF migration history.
    /// </summary>
    public static void Initialize(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClinicalDbContext>();
        db.Database.Migrate();
    }
}
