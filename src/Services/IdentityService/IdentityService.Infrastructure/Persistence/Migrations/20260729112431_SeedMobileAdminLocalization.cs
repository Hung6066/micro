using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional
#pragma warning disable CA1861 // Migration seed arrays are deployment artifacts

namespace His.Hope.IdentityService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeedMobileAdminLocalization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "localization_resources",
                columns: new[] { "key", "description" },
                values: new object[,]
                {
                    { "mobile.auth.signIn", "Mobile admin sign in action" },
                    { "mobile.auth.signOut", "Mobile admin sign out action" },
                    { "mobile.brand.identityAdministration", "Mobile admin identity service label" },
                    { "mobile.common.cancel", "Common cancel action" },
                    { "mobile.common.delete", "Common delete action" },
                    { "mobile.common.empty", "Common empty state" },
                    { "mobile.common.error", "Common error state" },
                    { "mobile.common.loading", "Common loading state" },
                    { "mobile.common.noPermission", "Common access denied state" },
                    { "mobile.common.refresh", "Common refresh action" },
                    { "mobile.common.retry", "Common retry action" },
                    { "mobile.common.save", "Common save action" },
                    { "mobile.common.search", "Common search action" },
                    { "mobile.mfa.alreadyEnabled", "MFA enabled status" },
                    { "mobile.mfa.createPasskey", "Passkey registration action" },
                    { "mobile.mfa.passkeyRegistrationFailed", "Passkey registration error" },
                    { "mobile.mfa.setupFailed", "MFA setup error" },
                    { "mobile.mfa.title", "MFA security screen title" },
                    { "mobile.mfa.verify", "MFA verification action" },
                    { "mobile.nav.clients", "Mobile admin navigation: clients" },
                    { "mobile.nav.consents", "Mobile admin navigation: consents" },
                    { "mobile.nav.home", "Mobile admin navigation: home" },
                    { "mobile.nav.roles", "Mobile admin navigation: roles" },
                    { "mobile.nav.settings", "Mobile admin navigation: settings" },
                    { "mobile.nav.users", "Mobile admin navigation: users" },
                    { "mobile.providers.ldap", "LDAP directory provider" },
                    { "mobile.providers.saml", "SAML identity provider" }
                });

            migrationBuilder.InsertData(
                table: "localization_translations",
                columns: new[] { "locale", "resource_key", "value" },
                values: new object[,]
                {
                    { "en-US", "mobile.auth.signIn", "Sign in" },
                    { "vi-VN", "mobile.auth.signIn", "Đăng nhập" },
                    { "en-US", "mobile.auth.signOut", "Sign out" },
                    { "vi-VN", "mobile.auth.signOut", "Đăng xuất" },
                    { "en-US", "mobile.brand.identityAdministration", "IDENTITY ADMINISTRATION" },
                    { "vi-VN", "mobile.brand.identityAdministration", "QUẢN TRỊ DANH TÍNH" },
                    { "en-US", "mobile.common.cancel", "Cancel" },
                    { "vi-VN", "mobile.common.cancel", "Hủy" },
                    { "en-US", "mobile.common.delete", "Delete" },
                    { "vi-VN", "mobile.common.delete", "Xóa" },
                    { "en-US", "mobile.common.empty", "No data available" },
                    { "vi-VN", "mobile.common.empty", "Chưa có dữ liệu" },
                    { "en-US", "mobile.common.error", "Something went wrong" },
                    { "vi-VN", "mobile.common.error", "Đã xảy ra lỗi" },
                    { "en-US", "mobile.common.loading", "Loading..." },
                    { "vi-VN", "mobile.common.loading", "Đang tải..." },
                    { "en-US", "mobile.common.noPermission", "Access denied" },
                    { "vi-VN", "mobile.common.noPermission", "Không có quyền truy cập" },
                    { "en-US", "mobile.common.refresh", "Refresh" },
                    { "vi-VN", "mobile.common.refresh", "Làm mới" },
                    { "en-US", "mobile.common.retry", "Retry" },
                    { "vi-VN", "mobile.common.retry", "Thử lại" },
                    { "en-US", "mobile.common.save", "Save" },
                    { "vi-VN", "mobile.common.save", "Lưu" },
                    { "en-US", "mobile.common.search", "Search" },
                    { "vi-VN", "mobile.common.search", "Tìm kiếm" },
                    { "en-US", "mobile.mfa.alreadyEnabled", "MFA is already enabled" },
                    { "vi-VN", "mobile.mfa.alreadyEnabled", "MFA đã được bật" },
                    { "en-US", "mobile.mfa.createPasskey", "Create passkey" },
                    { "vi-VN", "mobile.mfa.createPasskey", "Tạo passkey" },
                    { "en-US", "mobile.mfa.passkeyRegistrationFailed", "Passkey registration failed" },
                    { "vi-VN", "mobile.mfa.passkeyRegistrationFailed", "Đăng ký passkey thất bại" },
                    { "en-US", "mobile.mfa.setupFailed", "Unable to start MFA setup" },
                    { "vi-VN", "mobile.mfa.setupFailed", "Không thể bắt đầu thiết lập MFA" },
                    { "en-US", "mobile.mfa.title", "MFA security" },
                    { "vi-VN", "mobile.mfa.title", "Bảo mật MFA" },
                    { "en-US", "mobile.mfa.verify", "Verify" },
                    { "vi-VN", "mobile.mfa.verify", "Xác minh" },
                    { "en-US", "mobile.nav.clients", "Clients" },
                    { "vi-VN", "mobile.nav.clients", "Ứng dụng" },
                    { "en-US", "mobile.nav.consents", "Consents" },
                    { "vi-VN", "mobile.nav.consents", "Chấp thuận" },
                    { "en-US", "mobile.nav.home", "Home" },
                    { "vi-VN", "mobile.nav.home", "Trang chủ" },
                    { "en-US", "mobile.nav.roles", "Roles" },
                    { "vi-VN", "mobile.nav.roles", "Vai trò" },
                    { "en-US", "mobile.nav.settings", "Settings" },
                    { "vi-VN", "mobile.nav.settings", "Cài đặt" },
                    { "en-US", "mobile.nav.users", "Users" },
                    { "vi-VN", "mobile.nav.users", "Người dùng" },
                    { "en-US", "mobile.providers.ldap", "LDAP/AD" },
                    { "vi-VN", "mobile.providers.ldap", "LDAP/AD" },
                    { "en-US", "mobile.providers.saml", "SAML SSO" },
                    { "vi-VN", "mobile.providers.saml", "Đăng nhập SSO SAML" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key" },
                keyValues: new object[] { "en-US", "mobile.auth.signIn" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key" },
                keyValues: new object[] { "vi-VN", "mobile.auth.signIn" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key" },
                keyValues: new object[] { "en-US", "mobile.auth.signOut" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key" },
                keyValues: new object[] { "vi-VN", "mobile.auth.signOut" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key" },
                keyValues: new object[] { "en-US", "mobile.brand.identityAdministration" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key" },
                keyValues: new object[] { "vi-VN", "mobile.brand.identityAdministration" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key" },
                keyValues: new object[] { "en-US", "mobile.common.cancel" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key" },
                keyValues: new object[] { "vi-VN", "mobile.common.cancel" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key" },
                keyValues: new object[] { "en-US", "mobile.common.delete" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key" },
                keyValues: new object[] { "vi-VN", "mobile.common.delete" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key" },
                keyValues: new object[] { "en-US", "mobile.common.empty" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key" },
                keyValues: new object[] { "vi-VN", "mobile.common.empty" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key" },
                keyValues: new object[] { "en-US", "mobile.common.error" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key" },
                keyValues: new object[] { "vi-VN", "mobile.common.error" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key" },
                keyValues: new object[] { "en-US", "mobile.common.loading" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key" },
                keyValues: new object[] { "vi-VN", "mobile.common.loading" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key" },
                keyValues: new object[] { "en-US", "mobile.common.noPermission" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key" },
                keyValues: new object[] { "vi-VN", "mobile.common.noPermission" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key" },
                keyValues: new object[] { "en-US", "mobile.common.refresh" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key" },
                keyValues: new object[] { "vi-VN", "mobile.common.refresh" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key" },
                keyValues: new object[] { "en-US", "mobile.common.retry" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key" },
                keyValues: new object[] { "vi-VN", "mobile.common.retry" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key" },
                keyValues: new object[] { "en-US", "mobile.common.save" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key" },
                keyValues: new object[] { "vi-VN", "mobile.common.save" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key" },
                keyValues: new object[] { "en-US", "mobile.common.search" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key" },
                keyValues: new object[] { "vi-VN", "mobile.common.search" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key" },
                keyValues: new object[] { "en-US", "mobile.mfa.alreadyEnabled" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key" },
                keyValues: new object[] { "vi-VN", "mobile.mfa.alreadyEnabled" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key" },
                keyValues: new object[] { "en-US", "mobile.mfa.createPasskey" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key" },
                keyValues: new object[] { "vi-VN", "mobile.mfa.createPasskey" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key" },
                keyValues: new object[] { "en-US", "mobile.mfa.passkeyRegistrationFailed" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key" },
                keyValues: new object[] { "vi-VN", "mobile.mfa.passkeyRegistrationFailed" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key" },
                keyValues: new object[] { "en-US", "mobile.mfa.setupFailed" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key" },
                keyValues: new object[] { "vi-VN", "mobile.mfa.setupFailed" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key" },
                keyValues: new object[] { "en-US", "mobile.mfa.title" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key" },
                keyValues: new object[] { "vi-VN", "mobile.mfa.title" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key" },
                keyValues: new object[] { "en-US", "mobile.mfa.verify" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key" },
                keyValues: new object[] { "vi-VN", "mobile.mfa.verify" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key" },
                keyValues: new object[] { "en-US", "mobile.nav.clients" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key" },
                keyValues: new object[] { "vi-VN", "mobile.nav.clients" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key" },
                keyValues: new object[] { "en-US", "mobile.nav.consents" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key" },
                keyValues: new object[] { "vi-VN", "mobile.nav.consents" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key" },
                keyValues: new object[] { "en-US", "mobile.nav.home" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key" },
                keyValues: new object[] { "vi-VN", "mobile.nav.home" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key" },
                keyValues: new object[] { "en-US", "mobile.nav.roles" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key" },
                keyValues: new object[] { "vi-VN", "mobile.nav.roles" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key" },
                keyValues: new object[] { "en-US", "mobile.nav.settings" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key" },
                keyValues: new object[] { "vi-VN", "mobile.nav.settings" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key" },
                keyValues: new object[] { "en-US", "mobile.nav.users" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key" },
                keyValues: new object[] { "vi-VN", "mobile.nav.users" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key" },
                keyValues: new object[] { "en-US", "mobile.providers.ldap" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key" },
                keyValues: new object[] { "vi-VN", "mobile.providers.ldap" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key" },
                keyValues: new object[] { "en-US", "mobile.providers.saml" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key" },
                keyValues: new object[] { "vi-VN", "mobile.providers.saml" });

            migrationBuilder.DeleteData(
                table: "localization_resources",
                keyColumn: "key",
                keyValue: "mobile.auth.signIn");

            migrationBuilder.DeleteData(
                table: "localization_resources",
                keyColumn: "key",
                keyValue: "mobile.auth.signOut");

            migrationBuilder.DeleteData(
                table: "localization_resources",
                keyColumn: "key",
                keyValue: "mobile.brand.identityAdministration");

            migrationBuilder.DeleteData(
                table: "localization_resources",
                keyColumn: "key",
                keyValue: "mobile.common.cancel");

            migrationBuilder.DeleteData(
                table: "localization_resources",
                keyColumn: "key",
                keyValue: "mobile.common.delete");

            migrationBuilder.DeleteData(
                table: "localization_resources",
                keyColumn: "key",
                keyValue: "mobile.common.empty");

            migrationBuilder.DeleteData(
                table: "localization_resources",
                keyColumn: "key",
                keyValue: "mobile.common.error");

            migrationBuilder.DeleteData(
                table: "localization_resources",
                keyColumn: "key",
                keyValue: "mobile.common.loading");

            migrationBuilder.DeleteData(
                table: "localization_resources",
                keyColumn: "key",
                keyValue: "mobile.common.noPermission");

            migrationBuilder.DeleteData(
                table: "localization_resources",
                keyColumn: "key",
                keyValue: "mobile.common.refresh");

            migrationBuilder.DeleteData(
                table: "localization_resources",
                keyColumn: "key",
                keyValue: "mobile.common.retry");

            migrationBuilder.DeleteData(
                table: "localization_resources",
                keyColumn: "key",
                keyValue: "mobile.common.save");

            migrationBuilder.DeleteData(
                table: "localization_resources",
                keyColumn: "key",
                keyValue: "mobile.common.search");

            migrationBuilder.DeleteData(
                table: "localization_resources",
                keyColumn: "key",
                keyValue: "mobile.mfa.alreadyEnabled");

            migrationBuilder.DeleteData(
                table: "localization_resources",
                keyColumn: "key",
                keyValue: "mobile.mfa.createPasskey");

            migrationBuilder.DeleteData(
                table: "localization_resources",
                keyColumn: "key",
                keyValue: "mobile.mfa.passkeyRegistrationFailed");

            migrationBuilder.DeleteData(
                table: "localization_resources",
                keyColumn: "key",
                keyValue: "mobile.mfa.setupFailed");

            migrationBuilder.DeleteData(
                table: "localization_resources",
                keyColumn: "key",
                keyValue: "mobile.mfa.title");

            migrationBuilder.DeleteData(
                table: "localization_resources",
                keyColumn: "key",
                keyValue: "mobile.mfa.verify");

            migrationBuilder.DeleteData(
                table: "localization_resources",
                keyColumn: "key",
                keyValue: "mobile.nav.clients");

            migrationBuilder.DeleteData(
                table: "localization_resources",
                keyColumn: "key",
                keyValue: "mobile.nav.consents");

            migrationBuilder.DeleteData(
                table: "localization_resources",
                keyColumn: "key",
                keyValue: "mobile.nav.home");

            migrationBuilder.DeleteData(
                table: "localization_resources",
                keyColumn: "key",
                keyValue: "mobile.nav.roles");

            migrationBuilder.DeleteData(
                table: "localization_resources",
                keyColumn: "key",
                keyValue: "mobile.nav.settings");

            migrationBuilder.DeleteData(
                table: "localization_resources",
                keyColumn: "key",
                keyValue: "mobile.nav.users");

            migrationBuilder.DeleteData(
                table: "localization_resources",
                keyColumn: "key",
                keyValue: "mobile.providers.ldap");

            migrationBuilder.DeleteData(
                table: "localization_resources",
                keyColumn: "key",
                keyValue: "mobile.providers.saml");
        }
    }
}
#pragma warning restore CA1861
