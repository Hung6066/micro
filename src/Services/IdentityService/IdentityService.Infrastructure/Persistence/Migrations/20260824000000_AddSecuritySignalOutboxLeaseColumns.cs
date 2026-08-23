using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace His.Hope.IdentityService.Infrastructure.Persistence.Migrations;

public partial class AddSecuritySignalOutboxLeaseColumns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "lease_id",
            table: "security_signal_outbox",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "lease_until",
            table: "security_signal_outbox",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "ix_security_signal_outbox_dispatched_at_lease_until_available_at",
            table: "security_signal_outbox",
            columns: new[] { "dispatched_at", "lease_until", "available_at" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_security_signal_outbox_dispatched_at_lease_until_available_at",
            table: "security_signal_outbox");

        migrationBuilder.DropColumn(name: "lease_id", table: "security_signal_outbox");
        migrationBuilder.DropColumn(name: "lease_until", table: "security_signal_outbox");
    }
}
