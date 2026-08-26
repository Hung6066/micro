using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using His.Hope.IdentityService.Infrastructure.Persistence;

#nullable disable

namespace His.Hope.IdentityService.Infrastructure.Persistence.Migrations;

[DbContext(typeof(IdentityDbContext))]
partial class AddSecuritySignalOutboxLeaseColumns
{
    // The migration operations are defined in the companion migration file.
    // Keeping the generated metadata type ensures EF discovers this migration
    // in the same way as the existing scaffolded migrations.
}
