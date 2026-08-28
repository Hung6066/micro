using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace His.Hope.IdentityService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StandardizeDataLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                table: "user_password_history",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                table: "user_password_history",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                table: "user_password_history",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deleted_by",
                table: "user_password_history",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "user_password_history",
                type: "boolean",
                nullable: true,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "user_password_history",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "updated_by",
                table: "user_password_history",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "user_mfa",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                table: "user_mfa",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                table: "user_mfa",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deleted_by",
                table: "user_mfa",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "user_mfa",
                type: "boolean",
                nullable: true,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "updated_by",
                table: "user_mfa",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "user_facilities",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                table: "user_facilities",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                table: "user_facilities",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deleted_by",
                table: "user_facilities",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "user_facilities",
                type: "boolean",
                nullable: true,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "user_facilities",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "updated_by",
                table: "user_facilities",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "user_client_certificates",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                table: "user_client_certificates",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                table: "user_client_certificates",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deleted_by",
                table: "user_client_certificates",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "user_client_certificates",
                type: "boolean",
                nullable: true,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "user_client_certificates",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "updated_by",
                table: "user_client_certificates",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "updated_by",
                table: "system_settings",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                table: "system_settings",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                table: "system_settings",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                table: "system_settings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deleted_by",
                table: "system_settings",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "system_settings",
                type: "boolean",
                nullable: true,
                defaultValue: false);

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "support_elevations",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                table: "support_elevations",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                table: "support_elevations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deleted_by",
                table: "support_elevations",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "support_elevations",
                type: "boolean",
                nullable: true,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "support_elevations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "updated_by",
                table: "support_elevations",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "security_signal_outbox",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                table: "security_signal_outbox",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                table: "security_signal_outbox",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deleted_by",
                table: "security_signal_outbox",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "security_signal_outbox",
                type: "boolean",
                nullable: true,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "security_signal_outbox",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "updated_by",
                table: "security_signal_outbox",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                table: "security_events",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                table: "security_events",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                table: "security_events",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deleted_by",
                table: "security_events",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "security_events",
                type: "boolean",
                nullable: true,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "security_events",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "updated_by",
                table: "security_events",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "role_template_versions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                table: "role_template_versions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deleted_by",
                table: "role_template_versions",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "role_template_versions",
                type: "boolean",
                nullable: true,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "role_template_versions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "updated_by",
                table: "role_template_versions",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                table: "role_permissions",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                table: "role_permissions",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                table: "role_permissions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deleted_by",
                table: "role_permissions",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "role_permissions",
                type: "boolean",
                nullable: true,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "role_permissions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "updated_by",
                table: "role_permissions",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "push_notification_outbox",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                table: "push_notification_outbox",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                table: "push_notification_outbox",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deleted_by",
                table: "push_notification_outbox",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "push_notification_outbox",
                type: "boolean",
                nullable: true,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "push_notification_outbox",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "updated_by",
                table: "push_notification_outbox",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "push_delivery_attempts",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                table: "push_delivery_attempts",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                table: "push_delivery_attempts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deleted_by",
                table: "push_delivery_attempts",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "push_delivery_attempts",
                type: "boolean",
                nullable: true,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "push_delivery_attempts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "updated_by",
                table: "push_delivery_attempts",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "permissions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                table: "permissions",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                table: "permissions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deleted_by",
                table: "permissions",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "permissions",
                type: "boolean",
                nullable: true,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "permissions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "updated_by",
                table: "permissions",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "passkey_credentials",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                table: "passkey_credentials",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                table: "passkey_credentials",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deleted_by",
                table: "passkey_credentials",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "passkey_credentials",
                type: "boolean",
                nullable: true,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "passkey_credentials",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "updated_by",
                table: "passkey_credentials",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                table: "openiddict_tokens",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                table: "openiddict_tokens",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                table: "openiddict_tokens",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deleted_by",
                table: "openiddict_tokens",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "openiddict_tokens",
                type: "boolean",
                nullable: true,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "openiddict_tokens",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "updated_by",
                table: "openiddict_tokens",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                table: "openiddict_scopes",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                table: "openiddict_scopes",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                table: "openiddict_scopes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deleted_by",
                table: "openiddict_scopes",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "openiddict_scopes",
                type: "boolean",
                nullable: true,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "openiddict_scopes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "updated_by",
                table: "openiddict_scopes",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                table: "openiddict_consents",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                table: "openiddict_consents",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                table: "openiddict_consents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deleted_by",
                table: "openiddict_consents",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "openiddict_consents",
                type: "boolean",
                nullable: true,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "openiddict_consents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "updated_by",
                table: "openiddict_consents",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                table: "openiddict_authorizations",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                table: "openiddict_authorizations",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                table: "openiddict_authorizations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deleted_by",
                table: "openiddict_authorizations",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "openiddict_authorizations",
                type: "boolean",
                nullable: true,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "openiddict_authorizations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "updated_by",
                table: "openiddict_authorizations",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                table: "openiddict_applications",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                table: "openiddict_applications",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                table: "openiddict_applications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deleted_by",
                table: "openiddict_applications",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "openiddict_applications",
                type: "boolean",
                nullable: true,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "openiddict_applications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "updated_by",
                table: "openiddict_applications",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "mobile_telemetry_events",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                table: "mobile_telemetry_events",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                table: "mobile_telemetry_events",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deleted_by",
                table: "mobile_telemetry_events",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "mobile_telemetry_events",
                type: "boolean",
                nullable: true,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "mobile_telemetry_events",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "updated_by",
                table: "mobile_telemetry_events",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                table: "mobile_device_registrations",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                table: "mobile_device_registrations",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                table: "mobile_device_registrations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deleted_by",
                table: "mobile_device_registrations",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "mobile_device_registrations",
                type: "boolean",
                nullable: true,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "mobile_device_registrations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "updated_by",
                table: "mobile_device_registrations",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                table: "localization_translations",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                table: "localization_translations",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                table: "localization_translations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deleted_by",
                table: "localization_translations",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "localization_translations",
                type: "boolean",
                nullable: true,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "localization_translations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "updated_by",
                table: "localization_translations",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                table: "localization_resources",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                table: "localization_resources",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                table: "localization_resources",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deleted_by",
                table: "localization_resources",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "localization_resources",
                type: "boolean",
                nullable: true,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "localization_resources",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "updated_by",
                table: "localization_resources",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "in_app_notifications",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                table: "in_app_notifications",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                table: "in_app_notifications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deleted_by",
                table: "in_app_notifications",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "in_app_notifications",
                type: "boolean",
                nullable: true,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "in_app_notifications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "updated_by",
                table: "in_app_notifications",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "iam_workload_roles",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                table: "iam_workload_roles",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                table: "iam_workload_roles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deleted_by",
                table: "iam_workload_roles",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "iam_workload_roles",
                type: "boolean",
                nullable: true,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "iam_workload_roles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "updated_by",
                table: "iam_workload_roles",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "iam_service_definitions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                table: "iam_service_definitions",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                table: "iam_service_definitions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deleted_by",
                table: "iam_service_definitions",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "iam_service_definitions",
                type: "boolean",
                nullable: true,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "iam_service_definitions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "updated_by",
                table: "iam_service_definitions",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "iam_scopes",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                table: "iam_scopes",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                table: "iam_scopes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deleted_by",
                table: "iam_scopes",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "iam_scopes",
                type: "boolean",
                nullable: true,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "iam_scopes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "updated_by",
                table: "iam_scopes",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "created_by",
                table: "iam_resource_policies",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "iam_resource_policies",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                table: "iam_resource_policies",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deleted_by",
                table: "iam_resource_policies",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "iam_resource_policies",
                type: "boolean",
                nullable: true,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "iam_resource_policies",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "updated_by",
                table: "iam_resource_policies",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "created_by",
                table: "iam_permission_sets",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "iam_permission_sets",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                table: "iam_permission_sets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deleted_by",
                table: "iam_permission_sets",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "iam_permission_sets",
                type: "boolean",
                nullable: true,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "iam_permission_sets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "updated_by",
                table: "iam_permission_sets",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "created_by",
                table: "iam_permission_set_assignments",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "iam_permission_set_assignments",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                table: "iam_permission_set_assignments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deleted_by",
                table: "iam_permission_set_assignments",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "iam_permission_set_assignments",
                type: "boolean",
                nullable: true,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "iam_permission_set_assignments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "updated_by",
                table: "iam_permission_set_assignments",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "created_by",
                table: "iam_permission_boundaries",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "iam_permission_boundaries",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                table: "iam_permission_boundaries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deleted_by",
                table: "iam_permission_boundaries",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "iam_permission_boundaries",
                type: "boolean",
                nullable: true,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "iam_permission_boundaries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "updated_by",
                table: "iam_permission_boundaries",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "created_by",
                table: "iam_groups",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "iam_groups",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                table: "iam_groups",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deleted_by",
                table: "iam_groups",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "iam_groups",
                type: "boolean",
                nullable: true,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "iam_groups",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "updated_by",
                table: "iam_groups",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "created_by",
                table: "iam_group_memberships",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "iam_group_memberships",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                table: "iam_group_memberships",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deleted_by",
                table: "iam_group_memberships",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "iam_group_memberships",
                type: "boolean",
                nullable: true,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "iam_group_memberships",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "updated_by",
                table: "iam_group_memberships",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "directory_provisioning_outbox",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                table: "directory_provisioning_outbox",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                table: "directory_provisioning_outbox",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deleted_by",
                table: "directory_provisioning_outbox",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "directory_provisioning_outbox",
                type: "boolean",
                nullable: true,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "directory_provisioning_outbox",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "updated_by",
                table: "directory_provisioning_outbox",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "directory_provisioning_bindings",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                table: "directory_provisioning_bindings",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                table: "directory_provisioning_bindings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deleted_by",
                table: "directory_provisioning_bindings",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "directory_provisioning_bindings",
                type: "boolean",
                nullable: true,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "updated_by",
                table: "directory_provisioning_bindings",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                table: "device_posture_policies",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                table: "device_posture_policies",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                table: "device_posture_policies",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deleted_by",
                table: "device_posture_policies",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "device_posture_policies",
                type: "boolean",
                nullable: true,
                defaultValue: false);

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "device_posture_assessments",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                table: "device_posture_assessments",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                table: "device_posture_assessments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deleted_by",
                table: "device_posture_assessments",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "device_posture_assessments",
                type: "boolean",
                nullable: true,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "device_posture_assessments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "updated_by",
                table: "device_posture_assessments",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                table: "break_glass_requests",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                table: "break_glass_requests",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                table: "break_glass_requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deleted_by",
                table: "break_glass_requests",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "break_glass_requests",
                type: "boolean",
                nullable: true,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "break_glass_requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "updated_by",
                table: "break_glass_requests",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "authorization_policy_definitions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                table: "authorization_policy_definitions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deleted_by",
                table: "authorization_policy_definitions",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "authorization_policy_definitions",
                type: "boolean",
                nullable: true,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "authorization_policy_definitions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "updated_by",
                table: "authorization_policy_definitions",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                table: "audit_logs",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                table: "audit_logs",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                table: "audit_logs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deleted_by",
                table: "audit_logs",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "audit_logs",
                type: "boolean",
                nullable: true,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "audit_logs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "updated_by",
                table: "audit_logs",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "asp_net_users",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                table: "asp_net_users",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                table: "asp_net_users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deleted_by",
                table: "asp_net_users",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "asp_net_users",
                type: "boolean",
                nullable: true,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "asp_net_users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "updated_by",
                table: "asp_net_users",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                table: "asp_net_user_tokens",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                table: "asp_net_user_tokens",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                table: "asp_net_user_tokens",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deleted_by",
                table: "asp_net_user_tokens",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "asp_net_user_tokens",
                type: "boolean",
                nullable: true,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "asp_net_user_tokens",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "updated_by",
                table: "asp_net_user_tokens",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                table: "asp_net_user_roles",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                table: "asp_net_user_roles",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                table: "asp_net_user_roles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deleted_by",
                table: "asp_net_user_roles",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "asp_net_user_roles",
                type: "boolean",
                nullable: true,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "asp_net_user_roles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "updated_by",
                table: "asp_net_user_roles",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                table: "asp_net_user_logins",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                table: "asp_net_user_logins",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                table: "asp_net_user_logins",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deleted_by",
                table: "asp_net_user_logins",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "asp_net_user_logins",
                type: "boolean",
                nullable: true,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "asp_net_user_logins",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "updated_by",
                table: "asp_net_user_logins",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                table: "asp_net_user_claims",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                table: "asp_net_user_claims",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                table: "asp_net_user_claims",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deleted_by",
                table: "asp_net_user_claims",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "asp_net_user_claims",
                type: "boolean",
                nullable: true,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "asp_net_user_claims",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "updated_by",
                table: "asp_net_user_claims",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "asp_net_roles",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                table: "asp_net_roles",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                table: "asp_net_roles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deleted_by",
                table: "asp_net_roles",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "asp_net_roles",
                type: "boolean",
                nullable: true,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "asp_net_roles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "updated_by",
                table: "asp_net_roles",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                table: "asp_net_role_claims",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                table: "asp_net_role_claims",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                table: "asp_net_role_claims",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deleted_by",
                table: "asp_net_role_claims",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "asp_net_role_claims",
                type: "boolean",
                nullable: true,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "asp_net_role_claims",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "updated_by",
                table: "asp_net_role_claims",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "created_at",
                table: "admin_table_views",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP",
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                table: "admin_table_views",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                table: "admin_table_views",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deleted_by",
                table: "admin_table_views",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "admin_table_views",
                type: "boolean",
                nullable: true,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "updated_by",
                table: "admin_table_views",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "access_reviews",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                table: "access_reviews",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                table: "access_reviews",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deleted_by",
                table: "access_reviews",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "access_reviews",
                type: "boolean",
                nullable: true,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "access_reviews",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "updated_by",
                table: "access_reviews",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                table: "access_requests",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                table: "access_requests",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                table: "access_requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deleted_by",
                table: "access_requests",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "access_requests",
                type: "boolean",
                nullable: true,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "access_requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "updated_by",
                table: "access_requests",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "app.dashboard.login", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "app.dashboard.logout", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "app.dashboard.logs", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "app.dashboard.metrics", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "app.dashboard.openLogs", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "app.dashboard.openPalette", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "app.dashboard.openResources", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "app.dashboard.openTraces", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "app.dashboard.resources", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "app.dashboard.slo", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "app.dashboard.title", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "app.dashboard.toggleNav", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "app.dashboard.toggleTheme", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "app.dashboard.traces", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "app.dashboard.userMenu", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "app.dashboard.workspace", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "app.hishope.title", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "app.navigation.changed", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "app.navigation.openMenu", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "app.theme.toggle", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.alerts.critical", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.alerts.criticalAlert", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.alerts.info", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.alerts.noAlerts", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.alerts.systemAlerts", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.alerts.view", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.alerts.warning", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.auth.completingSignIn", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.dependencyGraph.degraded", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.dependencyGraph.healthy", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.dependencyGraph.title", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.healthTimeline.allHealthy", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.healthTimeline.degraded", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.healthTimeline.down", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.healthTimeline.duration", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.healthTimeline.healthy", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.healthTimeline.incident", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.healthTimeline.incidents", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.healthTimeline.started", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.healthTimeline.title", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.healthTimeline.unknown", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.healthTimeline.waiting", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.logs.allServices", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.logs.clearFilters", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.logs.exception", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.logs.fullTextSearch", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.logs.level", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.logs.loading", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.logs.loadMore", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.logs.message", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.logs.noLogs", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.logs.pageTitle", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.logs.properties", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.logs.refresh", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.logs.results", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.logs.retry", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.logs.searchBtn", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.logs.searchPlaceholder", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.logs.service", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.logs.spanId", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.logs.tabSearch", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.logs.tabStream", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.logs.time", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.logs.timeRange", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.logs.traceId", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.logStream.autoScrollOn", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.logStream.clear", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.logStream.clickStartToBegin", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.logStream.disconnected", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.logStream.entries", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.logStream.following", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.logStream.newRecords", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.logStream.pause", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.logStream.realTime", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.logStream.scrollToBottom", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.logStream.start", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.logStream.streaming", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.metrics.apply", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.metrics.emptyState", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.metrics.live", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.metrics.loading", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.metrics.metricType", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.metrics.pageTitle", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.metrics.refresh", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.metrics.retry", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.metrics.selectServiceHint", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.metrics.service", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.metrics.servicesSelected", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.metrics.timeRange", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.metrics.timeRangeLabel", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.metricsOverview.degraded", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.metricsOverview.running", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.metricsOverview.stopped", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.metricsOverview.totalServices", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.resources.basicInfo", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.resources.cards", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.resources.close", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.resources.endpoints", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.resources.envVars", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.resources.graph", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.resources.health", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.resources.healthChecks", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.resources.loading", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.resources.name", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.resources.noResources", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.resources.operations", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.resources.refresh", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.resources.refreshing", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.resources.replicas", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.resources.retry", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.resources.status", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.resources.title", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.resources.version", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.slo.availability", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.slo.burn1h", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.slo.burn6h", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.slo.emptyState", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.slo.errorBudget", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.slo.last24h", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.slo.latencyTrend", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.slo.loading", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.slo.p99Latency", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.slo.pageTitle", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.slo.refresh", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.slo.retry", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.time.ago", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.time.daysAgo", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.time.hoursAgo", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.time.minutesAgo", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.traces.back", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.traces.detailTitle", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.traces.duration", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.traces.emptyState", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.traces.loading", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.traces.logs", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.traces.operation", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.traces.parentSpanId", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.traces.refresh", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.traces.retry", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.traces.service", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.traces.spanId", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.traces.spans", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.traces.startTime", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.traces.tags", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.traces.title", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.traces.traceId", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "dashboard.traces.viewDetail", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.auth.signIn", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.auth.signOut", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.brand.identityAdministration", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.common.cancel", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.common.delete", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.common.empty", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.common.error", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.common.loading", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.common.noPermission", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.common.refresh", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.common.retry", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.common.save", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.common.search", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.mfa.alreadyEnabled", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.mfa.createPasskey", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.mfa.passkeyRegistrationFailed", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.mfa.setupFailed", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.mfa.title", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.mfa.verify", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.nav.clients", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.nav.consents", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.nav.home", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.nav.roles", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.nav.settings", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.nav.users", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.operatorAccountMenu", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.operatorBatch", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.operatorChooseBatch", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.operatorCompleteWorkOrder", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.operatorFieldOperations", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.operatorIdentity", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.operatorInspector", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.operatorInspectorPlaceholder", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.operatorIsolationChecklist", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.operatorLotId", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.operatorLotPlaceholder", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.operatorMachineId", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.operatorMaintenance", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.operatorMaintenanceDescription", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.operatorMaintenanceEyebrow", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.operatorMaintenanceTitle", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.operatorMoisture", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.operatorNoTenantClaim", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.operatorOnline", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.operatorOutputQuantity", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.operatorProduction", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.operatorProductionDescription", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.operatorProductionTitle", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.operatorQuality", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.operatorQualityDescription", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.operatorQualityEyebrow", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.operatorQualityTitle", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.operatorRecordOperation", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.operatorSaveInspection", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.operatorStatus", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.operatorSync", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.operatorTechnician", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.operatorTenant", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.operatorTraceability", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.operatorWorkOrderId", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.providers.ldap", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.providers.saml", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.auth.signIn", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.auth.signIn", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.auth.signOut", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.auth.signOut", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.brand.identityAdministration", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.brand.identityAdministration", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.common.cancel", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.common.cancel", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.common.delete", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.common.delete", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.common.empty", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.common.empty", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.common.error", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.common.error", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.common.loading", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.common.loading", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.common.noPermission", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.common.noPermission", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.common.refresh", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.common.refresh", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.common.retry", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.common.retry", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.common.save", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.common.save", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.common.search", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.common.search", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.mfa.alreadyEnabled", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.mfa.alreadyEnabled", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.mfa.createPasskey", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.mfa.createPasskey", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.mfa.passkeyRegistrationFailed", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.mfa.passkeyRegistrationFailed", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.mfa.setupFailed", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.mfa.setupFailed", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.mfa.title", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.mfa.title", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.mfa.verify", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.mfa.verify", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.nav.clients", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.nav.clients", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.nav.consents", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.nav.consents", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.nav.home", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.nav.home", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.nav.roles", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.nav.roles", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.nav.settings", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.nav.settings", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.nav.users", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.nav.users", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.operatorAccountMenu", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.operatorAccountMenu", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.operatorBatch", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.operatorBatch", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.operatorChooseBatch", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.operatorChooseBatch", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.operatorCompleteWorkOrder", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.operatorCompleteWorkOrder", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.operatorFieldOperations", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.operatorFieldOperations", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.operatorIdentity", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.operatorIdentity", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.operatorInspector", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.operatorInspector", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.operatorInspectorPlaceholder", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.operatorInspectorPlaceholder", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.operatorIsolationChecklist", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.operatorIsolationChecklist", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.operatorLotId", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.operatorLotId", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.operatorLotPlaceholder", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.operatorLotPlaceholder", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.operatorMachineId", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.operatorMachineId", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.operatorMaintenance", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.operatorMaintenance", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.operatorMaintenanceDescription", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.operatorMaintenanceDescription", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.operatorMaintenanceEyebrow", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.operatorMaintenanceEyebrow", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.operatorMaintenanceTitle", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.operatorMaintenanceTitle", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.operatorMoisture", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.operatorMoisture", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.operatorNoTenantClaim", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.operatorNoTenantClaim", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.operatorOnline", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.operatorOnline", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.operatorOutputQuantity", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.operatorOutputQuantity", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.operatorProduction", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.operatorProduction", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.operatorProductionDescription", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.operatorProductionDescription", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.operatorProductionTitle", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.operatorProductionTitle", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.operatorQuality", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.operatorQuality", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.operatorQualityDescription", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.operatorQualityDescription", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.operatorQualityEyebrow", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.operatorQualityEyebrow", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.operatorQualityTitle", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.operatorQualityTitle", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.operatorRecordOperation", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.operatorRecordOperation", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.operatorSaveInspection", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.operatorSaveInspection", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.operatorStatus", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.operatorStatus", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.operatorSync", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.operatorSync", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.operatorTechnician", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.operatorTechnician", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.operatorTenant", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.operatorTenant", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.operatorTraceability", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.operatorTraceability", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.operatorWorkOrderId", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.operatorWorkOrderId", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.providers.ldap", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.providers.ldap", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.providers.saml", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.providers.saml", "global" },
                columns: new[] { "created_by", "deleted_at", "deleted_by", "updated_at", "updated_by" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.CreateIndex(
                name: "ix_openiddict_tokens_status_expiration",
                table: "openiddict_tokens",
                columns: new[] { "status", "expiration_date" });

            migrationBuilder.CreateIndex(
                name: "ix_openiddict_tokens_subject_status_expiration",
                table: "openiddict_tokens",
                columns: new[] { "subject", "status", "expiration_date" });

            migrationBuilder.CreateIndex(
                name: "ix_openiddict_scopes_name",
                table: "openiddict_scopes",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "ix_openiddict_authorizations_subject_status",
                table: "openiddict_authorizations",
                columns: new[] { "subject", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_openiddict_applications_client_id",
                table: "openiddict_applications",
                column: "client_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_resource_lookup",
                table: "audit_logs",
                columns: new[] { "resource_type", "resource_id", "timestamp" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_user_timeline",
                table: "audit_logs",
                columns: new[] { "user_id", "timestamp" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_openiddict_tokens_status_expiration",
                table: "openiddict_tokens");

            migrationBuilder.DropIndex(
                name: "ix_openiddict_tokens_subject_status_expiration",
                table: "openiddict_tokens");

            migrationBuilder.DropIndex(
                name: "ix_openiddict_scopes_name",
                table: "openiddict_scopes");

            migrationBuilder.DropIndex(
                name: "ix_openiddict_authorizations_subject_status",
                table: "openiddict_authorizations");

            migrationBuilder.DropIndex(
                name: "ix_openiddict_applications_client_id",
                table: "openiddict_applications");

            migrationBuilder.DropIndex(
                name: "ix_audit_logs_resource_lookup",
                table: "audit_logs");

            migrationBuilder.DropIndex(
                name: "ix_audit_logs_user_timeline",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "user_password_history");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "user_password_history");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "user_password_history");

            migrationBuilder.DropColumn(
                name: "deleted_by",
                table: "user_password_history");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "user_password_history");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "user_password_history");

            migrationBuilder.DropColumn(
                name: "updated_by",
                table: "user_password_history");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "user_mfa");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "user_mfa");

            migrationBuilder.DropColumn(
                name: "deleted_by",
                table: "user_mfa");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "user_mfa");

            migrationBuilder.DropColumn(
                name: "updated_by",
                table: "user_mfa");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "user_facilities");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "user_facilities");

            migrationBuilder.DropColumn(
                name: "deleted_by",
                table: "user_facilities");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "user_facilities");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "user_facilities");

            migrationBuilder.DropColumn(
                name: "updated_by",
                table: "user_facilities");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "user_client_certificates");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "user_client_certificates");

            migrationBuilder.DropColumn(
                name: "deleted_by",
                table: "user_client_certificates");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "user_client_certificates");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "user_client_certificates");

            migrationBuilder.DropColumn(
                name: "updated_by",
                table: "user_client_certificates");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "system_settings");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "system_settings");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "system_settings");

            migrationBuilder.DropColumn(
                name: "deleted_by",
                table: "system_settings");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "system_settings");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "support_elevations");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "support_elevations");

            migrationBuilder.DropColumn(
                name: "deleted_by",
                table: "support_elevations");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "support_elevations");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "support_elevations");

            migrationBuilder.DropColumn(
                name: "updated_by",
                table: "support_elevations");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "security_signal_outbox");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "security_signal_outbox");

            migrationBuilder.DropColumn(
                name: "deleted_by",
                table: "security_signal_outbox");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "security_signal_outbox");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "security_signal_outbox");

            migrationBuilder.DropColumn(
                name: "updated_by",
                table: "security_signal_outbox");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "security_events");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "security_events");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "security_events");

            migrationBuilder.DropColumn(
                name: "deleted_by",
                table: "security_events");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "security_events");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "security_events");

            migrationBuilder.DropColumn(
                name: "updated_by",
                table: "security_events");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "role_template_versions");

            migrationBuilder.DropColumn(
                name: "deleted_by",
                table: "role_template_versions");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "role_template_versions");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "role_template_versions");

            migrationBuilder.DropColumn(
                name: "updated_by",
                table: "role_template_versions");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "role_permissions");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "role_permissions");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "role_permissions");

            migrationBuilder.DropColumn(
                name: "deleted_by",
                table: "role_permissions");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "role_permissions");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "role_permissions");

            migrationBuilder.DropColumn(
                name: "updated_by",
                table: "role_permissions");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "push_notification_outbox");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "push_notification_outbox");

            migrationBuilder.DropColumn(
                name: "deleted_by",
                table: "push_notification_outbox");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "push_notification_outbox");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "push_notification_outbox");

            migrationBuilder.DropColumn(
                name: "updated_by",
                table: "push_notification_outbox");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "push_delivery_attempts");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "push_delivery_attempts");

            migrationBuilder.DropColumn(
                name: "deleted_by",
                table: "push_delivery_attempts");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "push_delivery_attempts");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "push_delivery_attempts");

            migrationBuilder.DropColumn(
                name: "updated_by",
                table: "push_delivery_attempts");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "permissions");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "permissions");

            migrationBuilder.DropColumn(
                name: "deleted_by",
                table: "permissions");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "permissions");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "permissions");

            migrationBuilder.DropColumn(
                name: "updated_by",
                table: "permissions");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "passkey_credentials");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "passkey_credentials");

            migrationBuilder.DropColumn(
                name: "deleted_by",
                table: "passkey_credentials");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "passkey_credentials");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "passkey_credentials");

            migrationBuilder.DropColumn(
                name: "updated_by",
                table: "passkey_credentials");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "openiddict_tokens");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "openiddict_tokens");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "openiddict_tokens");

            migrationBuilder.DropColumn(
                name: "deleted_by",
                table: "openiddict_tokens");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "openiddict_tokens");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "openiddict_tokens");

            migrationBuilder.DropColumn(
                name: "updated_by",
                table: "openiddict_tokens");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "openiddict_scopes");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "openiddict_scopes");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "openiddict_scopes");

            migrationBuilder.DropColumn(
                name: "deleted_by",
                table: "openiddict_scopes");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "openiddict_scopes");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "openiddict_scopes");

            migrationBuilder.DropColumn(
                name: "updated_by",
                table: "openiddict_scopes");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "openiddict_consents");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "openiddict_consents");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "openiddict_consents");

            migrationBuilder.DropColumn(
                name: "deleted_by",
                table: "openiddict_consents");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "openiddict_consents");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "openiddict_consents");

            migrationBuilder.DropColumn(
                name: "updated_by",
                table: "openiddict_consents");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "openiddict_authorizations");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "openiddict_authorizations");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "openiddict_authorizations");

            migrationBuilder.DropColumn(
                name: "deleted_by",
                table: "openiddict_authorizations");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "openiddict_authorizations");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "openiddict_authorizations");

            migrationBuilder.DropColumn(
                name: "updated_by",
                table: "openiddict_authorizations");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "openiddict_applications");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "openiddict_applications");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "openiddict_applications");

            migrationBuilder.DropColumn(
                name: "deleted_by",
                table: "openiddict_applications");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "openiddict_applications");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "openiddict_applications");

            migrationBuilder.DropColumn(
                name: "updated_by",
                table: "openiddict_applications");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "mobile_telemetry_events");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "mobile_telemetry_events");

            migrationBuilder.DropColumn(
                name: "deleted_by",
                table: "mobile_telemetry_events");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "mobile_telemetry_events");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "mobile_telemetry_events");

            migrationBuilder.DropColumn(
                name: "updated_by",
                table: "mobile_telemetry_events");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "mobile_device_registrations");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "mobile_device_registrations");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "mobile_device_registrations");

            migrationBuilder.DropColumn(
                name: "deleted_by",
                table: "mobile_device_registrations");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "mobile_device_registrations");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "mobile_device_registrations");

            migrationBuilder.DropColumn(
                name: "updated_by",
                table: "mobile_device_registrations");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "localization_translations");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "localization_translations");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "localization_translations");

            migrationBuilder.DropColumn(
                name: "deleted_by",
                table: "localization_translations");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "localization_translations");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "localization_translations");

            migrationBuilder.DropColumn(
                name: "updated_by",
                table: "localization_translations");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "localization_resources");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "localization_resources");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "localization_resources");

            migrationBuilder.DropColumn(
                name: "deleted_by",
                table: "localization_resources");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "localization_resources");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "localization_resources");

            migrationBuilder.DropColumn(
                name: "updated_by",
                table: "localization_resources");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "in_app_notifications");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "in_app_notifications");

            migrationBuilder.DropColumn(
                name: "deleted_by",
                table: "in_app_notifications");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "in_app_notifications");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "in_app_notifications");

            migrationBuilder.DropColumn(
                name: "updated_by",
                table: "in_app_notifications");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "iam_workload_roles");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "iam_workload_roles");

            migrationBuilder.DropColumn(
                name: "deleted_by",
                table: "iam_workload_roles");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "iam_workload_roles");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "iam_workload_roles");

            migrationBuilder.DropColumn(
                name: "updated_by",
                table: "iam_workload_roles");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "iam_service_definitions");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "iam_service_definitions");

            migrationBuilder.DropColumn(
                name: "deleted_by",
                table: "iam_service_definitions");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "iam_service_definitions");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "iam_service_definitions");

            migrationBuilder.DropColumn(
                name: "updated_by",
                table: "iam_service_definitions");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "iam_scopes");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "iam_scopes");

            migrationBuilder.DropColumn(
                name: "deleted_by",
                table: "iam_scopes");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "iam_scopes");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "iam_scopes");

            migrationBuilder.DropColumn(
                name: "updated_by",
                table: "iam_scopes");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "iam_resource_policies");

            migrationBuilder.DropColumn(
                name: "deleted_by",
                table: "iam_resource_policies");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "iam_resource_policies");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "iam_resource_policies");

            migrationBuilder.DropColumn(
                name: "updated_by",
                table: "iam_resource_policies");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "iam_permission_sets");

            migrationBuilder.DropColumn(
                name: "deleted_by",
                table: "iam_permission_sets");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "iam_permission_sets");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "iam_permission_sets");

            migrationBuilder.DropColumn(
                name: "updated_by",
                table: "iam_permission_sets");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "iam_permission_set_assignments");

            migrationBuilder.DropColumn(
                name: "deleted_by",
                table: "iam_permission_set_assignments");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "iam_permission_set_assignments");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "iam_permission_set_assignments");

            migrationBuilder.DropColumn(
                name: "updated_by",
                table: "iam_permission_set_assignments");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "iam_permission_boundaries");

            migrationBuilder.DropColumn(
                name: "deleted_by",
                table: "iam_permission_boundaries");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "iam_permission_boundaries");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "iam_permission_boundaries");

            migrationBuilder.DropColumn(
                name: "updated_by",
                table: "iam_permission_boundaries");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "iam_groups");

            migrationBuilder.DropColumn(
                name: "deleted_by",
                table: "iam_groups");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "iam_groups");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "iam_groups");

            migrationBuilder.DropColumn(
                name: "updated_by",
                table: "iam_groups");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "iam_group_memberships");

            migrationBuilder.DropColumn(
                name: "deleted_by",
                table: "iam_group_memberships");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "iam_group_memberships");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "iam_group_memberships");

            migrationBuilder.DropColumn(
                name: "updated_by",
                table: "iam_group_memberships");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "directory_provisioning_outbox");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "directory_provisioning_outbox");

            migrationBuilder.DropColumn(
                name: "deleted_by",
                table: "directory_provisioning_outbox");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "directory_provisioning_outbox");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "directory_provisioning_outbox");

            migrationBuilder.DropColumn(
                name: "updated_by",
                table: "directory_provisioning_outbox");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "directory_provisioning_bindings");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "directory_provisioning_bindings");

            migrationBuilder.DropColumn(
                name: "deleted_by",
                table: "directory_provisioning_bindings");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "directory_provisioning_bindings");

            migrationBuilder.DropColumn(
                name: "updated_by",
                table: "directory_provisioning_bindings");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "device_posture_policies");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "device_posture_policies");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "device_posture_policies");

            migrationBuilder.DropColumn(
                name: "deleted_by",
                table: "device_posture_policies");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "device_posture_policies");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "device_posture_assessments");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "device_posture_assessments");

            migrationBuilder.DropColumn(
                name: "deleted_by",
                table: "device_posture_assessments");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "device_posture_assessments");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "device_posture_assessments");

            migrationBuilder.DropColumn(
                name: "updated_by",
                table: "device_posture_assessments");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "break_glass_requests");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "break_glass_requests");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "break_glass_requests");

            migrationBuilder.DropColumn(
                name: "deleted_by",
                table: "break_glass_requests");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "break_glass_requests");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "break_glass_requests");

            migrationBuilder.DropColumn(
                name: "updated_by",
                table: "break_glass_requests");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "authorization_policy_definitions");

            migrationBuilder.DropColumn(
                name: "deleted_by",
                table: "authorization_policy_definitions");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "authorization_policy_definitions");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "authorization_policy_definitions");

            migrationBuilder.DropColumn(
                name: "updated_by",
                table: "authorization_policy_definitions");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "deleted_by",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "updated_by",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "asp_net_users");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "asp_net_users");

            migrationBuilder.DropColumn(
                name: "deleted_by",
                table: "asp_net_users");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "asp_net_users");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "asp_net_users");

            migrationBuilder.DropColumn(
                name: "updated_by",
                table: "asp_net_users");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "asp_net_user_tokens");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "asp_net_user_tokens");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "asp_net_user_tokens");

            migrationBuilder.DropColumn(
                name: "deleted_by",
                table: "asp_net_user_tokens");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "asp_net_user_tokens");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "asp_net_user_tokens");

            migrationBuilder.DropColumn(
                name: "updated_by",
                table: "asp_net_user_tokens");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "asp_net_user_roles");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "asp_net_user_roles");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "asp_net_user_roles");

            migrationBuilder.DropColumn(
                name: "deleted_by",
                table: "asp_net_user_roles");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "asp_net_user_roles");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "asp_net_user_roles");

            migrationBuilder.DropColumn(
                name: "updated_by",
                table: "asp_net_user_roles");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "asp_net_user_logins");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "asp_net_user_logins");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "asp_net_user_logins");

            migrationBuilder.DropColumn(
                name: "deleted_by",
                table: "asp_net_user_logins");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "asp_net_user_logins");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "asp_net_user_logins");

            migrationBuilder.DropColumn(
                name: "updated_by",
                table: "asp_net_user_logins");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "asp_net_user_claims");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "asp_net_user_claims");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "asp_net_user_claims");

            migrationBuilder.DropColumn(
                name: "deleted_by",
                table: "asp_net_user_claims");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "asp_net_user_claims");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "asp_net_user_claims");

            migrationBuilder.DropColumn(
                name: "updated_by",
                table: "asp_net_user_claims");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "asp_net_roles");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "asp_net_roles");

            migrationBuilder.DropColumn(
                name: "deleted_by",
                table: "asp_net_roles");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "asp_net_roles");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "asp_net_roles");

            migrationBuilder.DropColumn(
                name: "updated_by",
                table: "asp_net_roles");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "asp_net_role_claims");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "asp_net_role_claims");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "asp_net_role_claims");

            migrationBuilder.DropColumn(
                name: "deleted_by",
                table: "asp_net_role_claims");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "asp_net_role_claims");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "asp_net_role_claims");

            migrationBuilder.DropColumn(
                name: "updated_by",
                table: "asp_net_role_claims");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "admin_table_views");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "admin_table_views");

            migrationBuilder.DropColumn(
                name: "deleted_by",
                table: "admin_table_views");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "admin_table_views");

            migrationBuilder.DropColumn(
                name: "updated_by",
                table: "admin_table_views");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "access_reviews");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "access_reviews");

            migrationBuilder.DropColumn(
                name: "deleted_by",
                table: "access_reviews");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "access_reviews");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "access_reviews");

            migrationBuilder.DropColumn(
                name: "updated_by",
                table: "access_reviews");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "access_requests");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "access_requests");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "access_requests");

            migrationBuilder.DropColumn(
                name: "deleted_by",
                table: "access_requests");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "access_requests");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "access_requests");

            migrationBuilder.DropColumn(
                name: "updated_by",
                table: "access_requests");

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "user_mfa",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "user_facilities",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "user_client_certificates",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AlterColumn<string>(
                name: "updated_by",
                table: "system_settings",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "support_elevations",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "security_signal_outbox",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "role_template_versions",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "push_notification_outbox",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "push_delivery_attempts",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "permissions",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "passkey_credentials",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "mobile_telemetry_events",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "in_app_notifications",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "iam_workload_roles",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "iam_service_definitions",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "iam_scopes",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AlterColumn<string>(
                name: "created_by",
                table: "iam_resource_policies",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "iam_resource_policies",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AlterColumn<string>(
                name: "created_by",
                table: "iam_permission_sets",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "iam_permission_sets",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AlterColumn<string>(
                name: "created_by",
                table: "iam_permission_set_assignments",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "iam_permission_set_assignments",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AlterColumn<string>(
                name: "created_by",
                table: "iam_permission_boundaries",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "iam_permission_boundaries",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AlterColumn<string>(
                name: "created_by",
                table: "iam_groups",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "iam_groups",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AlterColumn<string>(
                name: "created_by",
                table: "iam_group_memberships",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "iam_group_memberships",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "directory_provisioning_outbox",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "directory_provisioning_bindings",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "device_posture_assessments",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "authorization_policy_definitions",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "asp_net_users",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "asp_net_roles",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "created_at",
                table: "admin_table_views",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "access_reviews",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "CURRENT_TIMESTAMP");
        }
    }
}
