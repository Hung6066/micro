using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore;
using His.Hope.IdentityService.Infrastructure.Persistence;

#nullable disable

namespace His.Hope.IdentityService.Infrastructure.Persistence.Migrations;

[DbContext(typeof(IdentityDbContext))]
[Migration("20260827090000_AddEnterpriseIdentityIndexes")]
partial class AddEnterpriseIdentityIndexes
{
    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        // The model snapshot is maintained by the lifecycle migration. This
        // follow-up migration only executes idempotent SQL indexes.
    }
}
