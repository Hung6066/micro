using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManufacturingService.Api.Migrations;

[DbContext(typeof(ManufacturingDbContext))]
[Migration("20260825013000_AddProductionBatchInputs")]
partial class AddProductionBatchInputs
{
    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        // The authoritative model is maintained by ManufacturingDbContextModelSnapshot.
    }
}
