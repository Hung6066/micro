using His.Hope.IdentityService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace His.Hope.IdentityService.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Data-only migration. The admin BFF session exchange (his-hope-admin → tenant nacoms-group)
    /// requires a tenant_membership claim, which only the startup seeder grants and production never
    /// runs it. Two operator accounts created in admin-app therefore signed in but got 403 on
    /// /api/v1/auth/session/exchange. This grants membership only; roles and permissions stay managed
    /// (and audited) in admin-app. Idempotent; skips users that no longer exist.
    /// </summary>
    [DbContext(typeof(IdentityDbContext))]
    [Migration("20261123050000_GrantNacomsGroupMembershipToOperators")]
    public partial class GrantNacomsGroupMembershipToOperators : Migration
    {
        private const string UserIds = "'aca33785-eaef-4108-9b32-6ef6355383e2'::uuid, '3045e9a9-56ec-449a-8969-25f2a2ee5e30'::uuid";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                INSERT INTO asp_net_user_claims (user_id, claim_type, claim_value)
                SELECT u.id, 'tenant_membership', 'nacoms-group'
                FROM asp_net_users u
                WHERE u.id IN ({UserIds})
                  AND NOT EXISTS (
                      SELECT 1 FROM asp_net_user_claims c
                      WHERE c.user_id = u.id
                        AND c.claim_type = 'tenant_membership'
                        AND lower(c.claim_value) = 'nacoms-group');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                DELETE FROM asp_net_user_claims
                WHERE user_id IN ({UserIds})
                  AND claim_type = 'tenant_membership'
                  AND claim_value = 'nacoms-group';
                """);
        }
    }
}
