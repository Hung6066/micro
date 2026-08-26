using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace His.Hope.IdentityService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeedOperatorMobileLocalization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "support_elevations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    operator_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_tenant = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    target_tenant = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    permissions_json = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    requested_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    approved_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_support_elevations", x => x.id);
                });

            migrationBuilder.InsertData(
                table: "localization_resources",
                columns: new[] { "key", "scope_id", "description" },
                values: new object[,]
                {
                    { "mobile.operatorAccountMenu", "global", "Operator account menu" },
                    { "mobile.operatorBatch", "global", "Operator batch label" },
                    { "mobile.operatorChooseBatch", "global", "Operator batch selector" },
                    { "mobile.operatorCompleteWorkOrder", "global", "Operator complete work order action" },
                    { "mobile.operatorFieldOperations", "global", "Operator shell title" },
                    { "mobile.operatorIdentity", "global", "Operator identity label" },
                    { "mobile.operatorInspector", "global", "Operator inspector label" },
                    { "mobile.operatorInspectorPlaceholder", "global", "Operator inspector placeholder" },
                    { "mobile.operatorIsolationChecklist", "global", "Operator isolation checklist" },
                    { "mobile.operatorLotId", "global", "Operator lot identifier" },
                    { "mobile.operatorLotPlaceholder", "global", "Operator lot placeholder" },
                    { "mobile.operatorMachineId", "global", "Operator machine identifier" },
                    { "mobile.operatorMaintenance", "global", "Operator maintenance navigation" },
                    { "mobile.operatorMaintenanceDescription", "global", "Operator maintenance description" },
                    { "mobile.operatorMaintenanceEyebrow", "global", "Operator maintenance eyebrow" },
                    { "mobile.operatorMaintenanceTitle", "global", "Operator maintenance title" },
                    { "mobile.operatorMoisture", "global", "Operator moisture label" },
                    { "mobile.operatorNoTenantClaim", "global", "Operator missing tenant claim" },
                    { "mobile.operatorOnline", "global", "Operator online status" },
                    { "mobile.operatorOutputQuantity", "global", "Operator output quantity" },
                    { "mobile.operatorProduction", "global", "Operator production navigation" },
                    { "mobile.operatorProductionDescription", "global", "Operator production description" },
                    { "mobile.operatorProductionTitle", "global", "Operator production title" },
                    { "mobile.operatorQuality", "global", "Operator quality navigation" },
                    { "mobile.operatorQualityDescription", "global", "Operator quality description" },
                    { "mobile.operatorQualityEyebrow", "global", "Operator quality eyebrow" },
                    { "mobile.operatorQualityTitle", "global", "Operator quality title" },
                    { "mobile.operatorRecordOperation", "global", "Operator record operation action" },
                    { "mobile.operatorSaveInspection", "global", "Operator save inspection action" },
                    { "mobile.operatorStatus", "global", "Operator status label" },
                    { "mobile.operatorSync", "global", "Operator sync navigation" },
                    { "mobile.operatorTechnician", "global", "Operator technician label" },
                    { "mobile.operatorTenant", "global", "Operator tenant label" },
                    { "mobile.operatorTraceability", "global", "Operator traceability navigation" },
                    { "mobile.operatorWorkOrderId", "global", "Operator work order identifier" }
                });

            migrationBuilder.InsertData(
                table: "localization_translations",
                columns: new[] { "locale", "resource_key", "scope_id", "value" },
                values: new object[,]
                {
                    { "en-US", "mobile.operatorAccountMenu", "global", "Account menu" },
                    { "vi-VN", "mobile.operatorAccountMenu", "global", "Menu tài khoản" },
                    { "en-US", "mobile.operatorBatch", "global", "Batch" },
                    { "vi-VN", "mobile.operatorBatch", "global", "Lô sản xuất" },
                    { "en-US", "mobile.operatorChooseBatch", "global", "Choose a started batch" },
                    { "vi-VN", "mobile.operatorChooseBatch", "global", "Chọn lô đã bắt đầu" },
                    { "en-US", "mobile.operatorCompleteWorkOrder", "global", "Complete work order" },
                    { "vi-VN", "mobile.operatorCompleteWorkOrder", "global", "Hoàn tất lệnh công việc" },
                    { "en-US", "mobile.operatorFieldOperations", "global", "Field operations" },
                    { "vi-VN", "mobile.operatorFieldOperations", "global", "Vận hành hiện trường" },
                    { "en-US", "mobile.operatorIdentity", "global", "Operator" },
                    { "vi-VN", "mobile.operatorIdentity", "global", "Operator" },
                    { "en-US", "mobile.operatorInspector", "global", "Inspector" },
                    { "vi-VN", "mobile.operatorInspector", "global", "Người kiểm tra" },
                    { "en-US", "mobile.operatorInspectorPlaceholder", "global", "Your name" },
                    { "vi-VN", "mobile.operatorInspectorPlaceholder", "global", "Tên của bạn" },
                    { "en-US", "mobile.operatorIsolationChecklist", "global", "Isolation checklist complete" },
                    { "vi-VN", "mobile.operatorIsolationChecklist", "global", "Đã hoàn tất checklist cô lập" },
                    { "en-US", "mobile.operatorLotId", "global", "Lot ID" },
                    { "vi-VN", "mobile.operatorLotId", "global", "Mã lô" },
                    { "en-US", "mobile.operatorLotPlaceholder", "global", "Lot identifier" },
                    { "vi-VN", "mobile.operatorLotPlaceholder", "global", "Mã nhận diện lô" },
                    { "en-US", "mobile.operatorMachineId", "global", "Machine ID" },
                    { "vi-VN", "mobile.operatorMachineId", "global", "Mã máy" },
                    { "en-US", "mobile.operatorMaintenance", "global", "Maintenance" },
                    { "vi-VN", "mobile.operatorMaintenance", "global", "Bảo trì" },
                    { "en-US", "mobile.operatorMaintenanceDescription", "global", "Complete assigned work orders with a checklist and field evidence, online or offline." },
                    { "vi-VN", "mobile.operatorMaintenanceDescription", "global", "Hoàn tất lệnh công việc với checklist và bằng chứng tại hiện trường, online hoặc offline." },
                    { "en-US", "mobile.operatorMaintenanceEyebrow", "global", "ASSET CARE" },
                    { "vi-VN", "mobile.operatorMaintenanceEyebrow", "global", "BẢO DƯỠNG THIẾT BỊ" },
                    { "en-US", "mobile.operatorMaintenanceTitle", "global", "Complete maintenance" },
                    { "vi-VN", "mobile.operatorMaintenanceTitle", "global", "Hoàn tất bảo trì" },
                    { "en-US", "mobile.operatorMoisture", "global", "Moisture %" },
                    { "vi-VN", "mobile.operatorMoisture", "global", "Độ ẩm %" },
                    { "en-US", "mobile.operatorNoTenantClaim", "global", "No tenant claim" },
                    { "vi-VN", "mobile.operatorNoTenantClaim", "global", "Chưa có tenant claim" },
                    { "en-US", "mobile.operatorOnline", "global", "Online" },
                    { "vi-VN", "mobile.operatorOnline", "global", "Trực tuyến" },
                    { "en-US", "mobile.operatorOutputQuantity", "global", "Output quantity" },
                    { "vi-VN", "mobile.operatorOutputQuantity", "global", "Sản lượng" },
                    { "en-US", "mobile.operatorProduction", "global", "Production" },
                    { "vi-VN", "mobile.operatorProduction", "global", "Sản xuất" },
                    { "en-US", "mobile.operatorProductionDescription", "global", "Record output at the point of work. Offline entries remain queued until synchronised." },
                    { "vi-VN", "mobile.operatorProductionDescription", "global", "Ghi nhận sản lượng tại điểm làm việc. Bản ghi ngoại tuyến sẽ chờ đồng bộ." },
                    { "en-US", "mobile.operatorProductionTitle", "global", "Production work" },
                    { "vi-VN", "mobile.operatorProductionTitle", "global", "Vận hành sản xuất" },
                    { "en-US", "mobile.operatorQuality", "global", "Quality" },
                    { "vi-VN", "mobile.operatorQuality", "global", "Chất lượng" },
                    { "en-US", "mobile.operatorQualityDescription", "global", "Capture a result against a lot. Pending records remain visible until synchronised." },
                    { "vi-VN", "mobile.operatorQualityDescription", "global", "Ghi nhận kết quả theo lô. Bản ghi chờ đồng bộ vẫn hiển thị." },
                    { "en-US", "mobile.operatorQualityEyebrow", "global", "QUALITY CONTROL" },
                    { "vi-VN", "mobile.operatorQualityEyebrow", "global", "KIỂM SOÁT CHẤT LƯỢNG" },
                    { "en-US", "mobile.operatorQualityTitle", "global", "Record inspection" },
                    { "vi-VN", "mobile.operatorQualityTitle", "global", "Ghi nhận kiểm tra" },
                    { "en-US", "mobile.operatorRecordOperation", "global", "Record operation" },
                    { "vi-VN", "mobile.operatorRecordOperation", "global", "Ghi nhận vận hành" },
                    { "en-US", "mobile.operatorSaveInspection", "global", "Save inspection" },
                    { "vi-VN", "mobile.operatorSaveInspection", "global", "Lưu kiểm tra" },
                    { "en-US", "mobile.operatorStatus", "global", "Status" },
                    { "vi-VN", "mobile.operatorStatus", "global", "Trạng thái" },
                    { "en-US", "mobile.operatorSync", "global", "Sync" },
                    { "vi-VN", "mobile.operatorSync", "global", "Đồng bộ" },
                    { "en-US", "mobile.operatorTechnician", "global", "Technician" },
                    { "vi-VN", "mobile.operatorTechnician", "global", "Kỹ thuật viên" },
                    { "en-US", "mobile.operatorTenant", "global", "Tenant" },
                    { "vi-VN", "mobile.operatorTenant", "global", "Tenant" },
                    { "en-US", "mobile.operatorTraceability", "global", "Traceability" },
                    { "vi-VN", "mobile.operatorTraceability", "global", "Truy xuất" },
                    { "en-US", "mobile.operatorWorkOrderId", "global", "Work order ID" },
                    { "vi-VN", "mobile.operatorWorkOrderId", "global", "Mã lệnh công việc" }
                });

            migrationBuilder.CreateIndex(
                name: "ix_support_elevations_operator_user_id_target_tenant_status_ex",
                table: "support_elevations",
                columns: new[] { "operator_user_id", "target_tenant", "status", "expires_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "support_elevations");

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.operatorAccountMenu", "global" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.operatorAccountMenu", "global" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.operatorBatch", "global" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.operatorBatch", "global" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.operatorChooseBatch", "global" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.operatorChooseBatch", "global" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.operatorCompleteWorkOrder", "global" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.operatorCompleteWorkOrder", "global" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.operatorFieldOperations", "global" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.operatorFieldOperations", "global" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.operatorIdentity", "global" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.operatorIdentity", "global" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.operatorInspector", "global" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.operatorInspector", "global" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.operatorInspectorPlaceholder", "global" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.operatorInspectorPlaceholder", "global" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.operatorIsolationChecklist", "global" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.operatorIsolationChecklist", "global" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.operatorLotId", "global" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.operatorLotId", "global" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.operatorLotPlaceholder", "global" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.operatorLotPlaceholder", "global" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.operatorMachineId", "global" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.operatorMachineId", "global" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.operatorMaintenance", "global" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.operatorMaintenance", "global" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.operatorMaintenanceDescription", "global" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.operatorMaintenanceDescription", "global" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.operatorMaintenanceEyebrow", "global" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.operatorMaintenanceEyebrow", "global" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.operatorMaintenanceTitle", "global" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.operatorMaintenanceTitle", "global" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.operatorMoisture", "global" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.operatorMoisture", "global" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.operatorNoTenantClaim", "global" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.operatorNoTenantClaim", "global" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.operatorOnline", "global" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.operatorOnline", "global" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.operatorOutputQuantity", "global" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.operatorOutputQuantity", "global" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.operatorProduction", "global" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.operatorProduction", "global" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.operatorProductionDescription", "global" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.operatorProductionDescription", "global" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.operatorProductionTitle", "global" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.operatorProductionTitle", "global" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.operatorQuality", "global" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.operatorQuality", "global" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.operatorQualityDescription", "global" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.operatorQualityDescription", "global" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.operatorQualityEyebrow", "global" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.operatorQualityEyebrow", "global" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.operatorQualityTitle", "global" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.operatorQualityTitle", "global" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.operatorRecordOperation", "global" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.operatorRecordOperation", "global" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.operatorSaveInspection", "global" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.operatorSaveInspection", "global" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.operatorStatus", "global" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.operatorStatus", "global" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.operatorSync", "global" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.operatorSync", "global" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.operatorTechnician", "global" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.operatorTechnician", "global" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.operatorTenant", "global" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.operatorTenant", "global" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.operatorTraceability", "global" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.operatorTraceability", "global" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "en-US", "mobile.operatorWorkOrderId", "global" });

            migrationBuilder.DeleteData(
                table: "localization_translations",
                keyColumns: new[] { "locale", "resource_key", "scope_id" },
                keyValues: new object[] { "vi-VN", "mobile.operatorWorkOrderId", "global" });

            migrationBuilder.DeleteData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.operatorAccountMenu", "global" });

            migrationBuilder.DeleteData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.operatorBatch", "global" });

            migrationBuilder.DeleteData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.operatorChooseBatch", "global" });

            migrationBuilder.DeleteData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.operatorCompleteWorkOrder", "global" });

            migrationBuilder.DeleteData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.operatorFieldOperations", "global" });

            migrationBuilder.DeleteData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.operatorIdentity", "global" });

            migrationBuilder.DeleteData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.operatorInspector", "global" });

            migrationBuilder.DeleteData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.operatorInspectorPlaceholder", "global" });

            migrationBuilder.DeleteData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.operatorIsolationChecklist", "global" });

            migrationBuilder.DeleteData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.operatorLotId", "global" });

            migrationBuilder.DeleteData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.operatorLotPlaceholder", "global" });

            migrationBuilder.DeleteData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.operatorMachineId", "global" });

            migrationBuilder.DeleteData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.operatorMaintenance", "global" });

            migrationBuilder.DeleteData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.operatorMaintenanceDescription", "global" });

            migrationBuilder.DeleteData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.operatorMaintenanceEyebrow", "global" });

            migrationBuilder.DeleteData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.operatorMaintenanceTitle", "global" });

            migrationBuilder.DeleteData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.operatorMoisture", "global" });

            migrationBuilder.DeleteData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.operatorNoTenantClaim", "global" });

            migrationBuilder.DeleteData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.operatorOnline", "global" });

            migrationBuilder.DeleteData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.operatorOutputQuantity", "global" });

            migrationBuilder.DeleteData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.operatorProduction", "global" });

            migrationBuilder.DeleteData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.operatorProductionDescription", "global" });

            migrationBuilder.DeleteData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.operatorProductionTitle", "global" });

            migrationBuilder.DeleteData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.operatorQuality", "global" });

            migrationBuilder.DeleteData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.operatorQualityDescription", "global" });

            migrationBuilder.DeleteData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.operatorQualityEyebrow", "global" });

            migrationBuilder.DeleteData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.operatorQualityTitle", "global" });

            migrationBuilder.DeleteData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.operatorRecordOperation", "global" });

            migrationBuilder.DeleteData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.operatorSaveInspection", "global" });

            migrationBuilder.DeleteData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.operatorStatus", "global" });

            migrationBuilder.DeleteData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.operatorSync", "global" });

            migrationBuilder.DeleteData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.operatorTechnician", "global" });

            migrationBuilder.DeleteData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.operatorTenant", "global" });

            migrationBuilder.DeleteData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.operatorTraceability", "global" });

            migrationBuilder.DeleteData(
                table: "localization_resources",
                keyColumns: new[] { "key", "scope_id" },
                keyValues: new object[] { "mobile.operatorWorkOrderId", "global" });
        }
    }
}
