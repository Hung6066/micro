using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManufacturingService.Api.Migrations;

[DbContext(typeof(ManufacturingDbContext))]
[Migration("20260825010000_AddProductionOutputLot")]
partial class AddProductionOutputLot
{
    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        // The authoritative model is maintained by ManufacturingDbContextModelSnapshot.
    }
}
