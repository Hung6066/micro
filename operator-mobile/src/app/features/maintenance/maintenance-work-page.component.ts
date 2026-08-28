import { ChangeDetectorRef, Component, effect, inject } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { OperationQueueService } from "../../core/offline/operation-queue.service";
import { OperatorMobileApiService, type Machine, type MachineCalibration, type MachineHealth, type MachineTelemetry, type MaintenancePlan, type MaintenanceWorkOrder } from "../../core/services/operator-mobile-api.service";
import { catchError, of } from "rxjs";
import { OperatorMobileTenantContextService } from "../../core/operator-mobile-tenant-context.service";
import { HisHopeI18nService, HisHopeTranslatePipe } from "@his-hope/frontend-foundation/i18n";
import { manufacturingEnumLabel } from "../../core/manufacturing-enum-label.util";

@Component({ standalone: true, imports: [FormsModule, HisHopeTranslatePipe], templateUrl: "./maintenance-work-page.component.html", styleUrls: ["./maintenance-work-page.component.scss"] })
export class MaintenanceWorkPageComponent {
  private readonly api = inject(OperatorMobileApiService);
  private readonly queue = inject(OperationQueueService);
  private readonly tenant = inject(OperatorMobileTenantContextService);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly cdr = inject(ChangeDetectorRef);
  checklistComplete = false;
  machineId = "";
  workOrderId = "";
  technician = "";
  evidence = "";
  message = "";
  loadError = "";
  machines: Machine[] = [];
  workOrders: MaintenanceWorkOrder[] = [];
  plans: MaintenancePlan[] = [];
  calibrations: MachineCalibration[] = [];
  telemetry: MachineTelemetry[] = [];
  health: MachineHealth | null = null;

  maintenanceStatusLabel(status: string): string { return manufacturingEnumLabel(this.i18n, "maintenanceStatus", status); }
  maintenanceTypeLabel(type: string): string { return manufacturingEnumLabel(this.i18n, "maintenanceType", type); }
  machineStateLabel(state: string): string { return manufacturingEnumLabel(this.i18n, "machineState", state); }
  calibrationResultLabel(result: string): string { return manufacturingEnumLabel(this.i18n, "qualityTestResult", result); }
  calibrationTypeLabel(type: string): string { return manufacturingEnumLabel(this.i18n, "calibrationTypes", type); }
  calibrationDueLabel(value: string): string { return this.i18n.formatDate(value, { dateStyle: "medium" }); }
  telemetryObservedLabel(value: string): string { return this.i18n.formatDateTime(value); }
  formatNumber(value: number): string { return this.i18n.formatNumber(value); }

  loadCalibrations(): void {
    const machineId = this.machineId.trim();
    if (!machineId) { this.calibrations = []; this.telemetry = []; return; }
    this.api.getMachineCalibrations(machineId).pipe(catchError(() => { this.loadError = this.i18n.t("mobile.operatorDataLoadFailed", "Unable to load operational data. Check your connection and permissions."); this.cdr.markForCheck(); return of([]); })).subscribe((calibrations) => {
      setTimeout(() => { this.calibrations = calibrations; this.cdr.markForCheck(); });
    });
    this.api.getMachineTelemetry(machineId).pipe(catchError(() => { this.loadError = this.i18n.t("mobile.operatorDataLoadFailed", "Unable to load operational data. Check your connection and permissions."); this.cdr.markForCheck(); return of([]); })).subscribe((telemetry) => {
      setTimeout(() => { this.telemetry = telemetry; this.cdr.markForCheck(); });
    });
  }

  constructor() {
    effect(() => {
      const tenantKey = this.tenant.activeTenantKey?.();
      if (!tenantKey) { this.machines = []; this.workOrders = []; this.plans = []; this.calibrations = []; this.telemetry = []; this.health = null; return; }
      this.api.getMachineHealth().pipe(catchError(() => { this.loadError = this.i18n.t("mobile.operatorDataLoadFailed", "Unable to load operational data. Check your connection and permissions."); this.cdr.markForCheck(); return of(null); })).subscribe((health) => {
        setTimeout(() => { this.health = health; this.cdr.markForCheck(); });
      });
      this.api.getMachines("Available").pipe(catchError(() => { this.loadError = this.i18n.t("mobile.operatorDataLoadFailed", "Unable to load operational data. Check your connection and permissions."); this.cdr.markForCheck(); return of([]); })).subscribe((machines) => {
        setTimeout(() => {
          this.machines = machines;
          if (this.machineId && !machines.some((machine) => machine.id === this.machineId)) this.machineId = "";
          this.loadCalibrations();
          this.cdr.markForCheck();
        });
      });
      this.api.getMaintenanceWorkOrders("Open").pipe(catchError(() => { this.loadError = this.i18n.t("mobile.operatorDataLoadFailed", "Unable to load operational data. Check your connection and permissions."); this.cdr.markForCheck(); return of([]); })).subscribe((orders) => {
        setTimeout(() => {
          this.workOrders = orders;
          if (this.workOrderId && !orders.some((order) => order.id === this.workOrderId)) this.workOrderId = "";
          this.cdr.markForCheck();
        });
      });
      this.api.getMaintenancePlans().pipe(catchError(() => { this.loadError = this.i18n.t("mobile.operatorDataLoadFailed", "Unable to load operational data. Check your connection and permissions."); this.cdr.markForCheck(); return of([]); })).subscribe((plans) => {
        setTimeout(() => { this.plans = plans; this.cdr.markForCheck(); });
      });
    });
  }

  async completeWorkOrder(): Promise<void> {
    if (!this.checklistComplete) {
      this.message = this.i18n.t("mobile.operatorChecklistRequired", "Complete the checklist before closing the work order.");
      return;
    }
    if (!this.machineId.trim() || !this.workOrderId.trim() || !this.technician.trim()) {
      this.message = this.i18n.t("mobile.operatorMaintenanceValidation", "Machine, work order and technician are required.");
      return;
    }
    const scope = this.tenant.commandScope;
    if (!scope) {
      this.message = this.i18n.t("mobile.operatorTenantRequired", "Sign in and select an operational tenant before completing maintenance.");
      return;
    }
    const operation = await this.queue.submit(
      { ...scope, endpoint: `/machines/${this.machineId}/maintenance-work-orders/${this.workOrderId}/complete`, payload: { technician: this.technician.trim(), completedAt: new Date().toISOString(), evidence: this.evidence.trim() || undefined } },
      (queued) => this.api.completeMaintenanceWorkOrder(queued),
    );
    this.message = operation.status === "synced" ? this.i18n.t("mobile.operatorMaintenanceCompleted", "Work order completed.") : this.i18n.t("mobile.operatorPendingSync", "Pending sync — it will retry when connected.");
  }
}
