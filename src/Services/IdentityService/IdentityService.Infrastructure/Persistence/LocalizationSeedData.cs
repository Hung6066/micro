using His.Hope.IdentityService.Domain.Entities;

namespace His.Hope.IdentityService.Infrastructure.Persistence;

/// <summary>
/// Baseline labels shared by the native mobile admin shell and its security screens.
/// The catalog is deliberately keyed by feature rather than by translated text so
/// Angular and native clients can consume the same API contract.
/// </summary>
internal static class LocalizationSeedData
{
    public static readonly LocalizationResource[] Resources =
    [
        new() { Key = "mobile.nav.home", Description = "Mobile admin navigation: home" },
        new() { Key = "mobile.nav.clients", Description = "Mobile admin navigation: clients" },
        new() { Key = "mobile.nav.users", Description = "Mobile admin navigation: users" },
        new() { Key = "mobile.nav.roles", Description = "Mobile admin navigation: roles" },
        new() { Key = "mobile.nav.consents", Description = "Mobile admin navigation: consents" },
        new() { Key = "mobile.nav.settings", Description = "Mobile admin navigation: settings" },
        new() { Key = "mobile.brand.identityAdministration", Description = "Mobile admin identity service label" },
        new() { Key = "mobile.auth.signIn", Description = "Mobile admin sign in action" },
        new() { Key = "mobile.auth.signOut", Description = "Mobile admin sign out action" },
        new() { Key = "mobile.common.search", Description = "Common search action" },
        new() { Key = "mobile.common.refresh", Description = "Common refresh action" },
        new() { Key = "mobile.common.retry", Description = "Common retry action" },
        new() { Key = "mobile.common.save", Description = "Common save action" },
        new() { Key = "mobile.common.cancel", Description = "Common cancel action" },
        new() { Key = "mobile.common.delete", Description = "Common delete action" },
        new() { Key = "mobile.common.loading", Description = "Common loading state" },
        new() { Key = "mobile.common.empty", Description = "Common empty state" },
        new() { Key = "mobile.common.error", Description = "Common error state" },
        new() { Key = "mobile.common.noPermission", Description = "Common access denied state" },
        new() { Key = "mobile.mfa.title", Description = "MFA security screen title" },
        new() { Key = "mobile.mfa.createPasskey", Description = "Passkey registration action" },
        new() { Key = "mobile.mfa.verify", Description = "MFA verification action" },
        new() { Key = "mobile.mfa.passkeyRegistrationFailed", Description = "Passkey registration error" },
        new() { Key = "mobile.mfa.setupFailed", Description = "MFA setup error" },
        new() { Key = "mobile.mfa.alreadyEnabled", Description = "MFA enabled status" },
        new() { Key = "mobile.providers.ldap", Description = "LDAP directory provider" },
        new() { Key = "mobile.providers.saml", Description = "SAML identity provider" }
    ];

    public static readonly LocalizationTranslation[] Translations =
    [
        new() { ResourceKey = "mobile.nav.home", Locale = "vi-VN", Value = "Trang chủ" },
        new() { ResourceKey = "mobile.nav.home", Locale = "en-US", Value = "Home" },
        new() { ResourceKey = "mobile.nav.clients", Locale = "vi-VN", Value = "Ứng dụng" },
        new() { ResourceKey = "mobile.nav.clients", Locale = "en-US", Value = "Clients" },
        new() { ResourceKey = "mobile.nav.users", Locale = "vi-VN", Value = "Người dùng" },
        new() { ResourceKey = "mobile.nav.users", Locale = "en-US", Value = "Users" },
        new() { ResourceKey = "mobile.nav.roles", Locale = "vi-VN", Value = "Vai trò" },
        new() { ResourceKey = "mobile.nav.roles", Locale = "en-US", Value = "Roles" },
        new() { ResourceKey = "mobile.nav.consents", Locale = "vi-VN", Value = "Chấp thuận" },
        new() { ResourceKey = "mobile.nav.consents", Locale = "en-US", Value = "Consents" },
        new() { ResourceKey = "mobile.nav.settings", Locale = "vi-VN", Value = "Cài đặt" },
        new() { ResourceKey = "mobile.nav.settings", Locale = "en-US", Value = "Settings" },
        new() { ResourceKey = "mobile.brand.identityAdministration", Locale = "vi-VN", Value = "QUẢN TRỊ DANH TÍNH" },
        new() { ResourceKey = "mobile.brand.identityAdministration", Locale = "en-US", Value = "IDENTITY ADMINISTRATION" },
        new() { ResourceKey = "mobile.auth.signIn", Locale = "vi-VN", Value = "Đăng nhập" },
        new() { ResourceKey = "mobile.auth.signIn", Locale = "en-US", Value = "Sign in" },
        new() { ResourceKey = "mobile.auth.signOut", Locale = "vi-VN", Value = "Đăng xuất" },
        new() { ResourceKey = "mobile.auth.signOut", Locale = "en-US", Value = "Sign out" },
        new() { ResourceKey = "mobile.common.search", Locale = "vi-VN", Value = "Tìm kiếm" },
        new() { ResourceKey = "mobile.common.search", Locale = "en-US", Value = "Search" },
        new() { ResourceKey = "mobile.common.refresh", Locale = "vi-VN", Value = "Làm mới" },
        new() { ResourceKey = "mobile.common.refresh", Locale = "en-US", Value = "Refresh" },
        new() { ResourceKey = "mobile.common.retry", Locale = "vi-VN", Value = "Thử lại" },
        new() { ResourceKey = "mobile.common.retry", Locale = "en-US", Value = "Retry" },
        new() { ResourceKey = "mobile.common.save", Locale = "vi-VN", Value = "Lưu" },
        new() { ResourceKey = "mobile.common.save", Locale = "en-US", Value = "Save" },
        new() { ResourceKey = "mobile.common.cancel", Locale = "vi-VN", Value = "Hủy" },
        new() { ResourceKey = "mobile.common.cancel", Locale = "en-US", Value = "Cancel" },
        new() { ResourceKey = "mobile.common.delete", Locale = "vi-VN", Value = "Xóa" },
        new() { ResourceKey = "mobile.common.delete", Locale = "en-US", Value = "Delete" },
        new() { ResourceKey = "mobile.common.loading", Locale = "vi-VN", Value = "Đang tải..." },
        new() { ResourceKey = "mobile.common.loading", Locale = "en-US", Value = "Loading..." },
        new() { ResourceKey = "mobile.common.empty", Locale = "vi-VN", Value = "Chưa có dữ liệu" },
        new() { ResourceKey = "mobile.common.empty", Locale = "en-US", Value = "No data available" },
        new() { ResourceKey = "mobile.common.error", Locale = "vi-VN", Value = "Đã xảy ra lỗi" },
        new() { ResourceKey = "mobile.common.error", Locale = "en-US", Value = "Something went wrong" },
        new() { ResourceKey = "mobile.common.noPermission", Locale = "vi-VN", Value = "Không có quyền truy cập" },
        new() { ResourceKey = "mobile.common.noPermission", Locale = "en-US", Value = "Access denied" },
        new() { ResourceKey = "mobile.mfa.title", Locale = "vi-VN", Value = "Bảo mật MFA" },
        new() { ResourceKey = "mobile.mfa.title", Locale = "en-US", Value = "MFA security" },
        new() { ResourceKey = "mobile.mfa.createPasskey", Locale = "vi-VN", Value = "Tạo passkey" },
        new() { ResourceKey = "mobile.mfa.createPasskey", Locale = "en-US", Value = "Create passkey" },
        new() { ResourceKey = "mobile.mfa.verify", Locale = "vi-VN", Value = "Xác minh" },
        new() { ResourceKey = "mobile.mfa.verify", Locale = "en-US", Value = "Verify" },
        new() { ResourceKey = "mobile.mfa.passkeyRegistrationFailed", Locale = "vi-VN", Value = "Đăng ký passkey thất bại" },
        new() { ResourceKey = "mobile.mfa.passkeyRegistrationFailed", Locale = "en-US", Value = "Passkey registration failed" },
        new() { ResourceKey = "mobile.mfa.setupFailed", Locale = "vi-VN", Value = "Không thể bắt đầu thiết lập MFA" },
        new() { ResourceKey = "mobile.mfa.setupFailed", Locale = "en-US", Value = "Unable to start MFA setup" },
        new() { ResourceKey = "mobile.mfa.alreadyEnabled", Locale = "vi-VN", Value = "MFA đã được bật" },
        new() { ResourceKey = "mobile.mfa.alreadyEnabled", Locale = "en-US", Value = "MFA is already enabled" },
        new() { ResourceKey = "mobile.providers.ldap", Locale = "vi-VN", Value = "LDAP/AD" },
        new() { ResourceKey = "mobile.providers.ldap", Locale = "en-US", Value = "LDAP/AD" },
        new() { ResourceKey = "mobile.providers.saml", Locale = "vi-VN", Value = "Đăng nhập SSO SAML" },
        new() { ResourceKey = "mobile.providers.saml", Locale = "en-US", Value = "SAML SSO" }
    ];
}
