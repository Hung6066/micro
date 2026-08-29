import { ChangeDetectorRef, Component, effect, inject } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { OperationQueueService } from "../../core/offline/operation-queue.service";
import { OperatorMobileApiService, type Machine, type MachineCalibration, type MachineHealth, type MachineTelemetry, type MaintenancePlan, type MaintenanceWorkOrder, type MachineDowntime } from "../../core/services/operator-mobile-api.service";
import { catchError, of } from "rxjs";
import { OperatorMobileTenantContextService } from "../../core/operator-mobile-tenant-context.service";
import { HisHopeI18nService, HisHopeTranslatePipe } from "@his-hope/frontend-foundation/i18n";
import { manufacturingEnumLabel } from "../../core/manufacturing-enum-label.util";
import { HisHopeSelectComponent } from "@his-hope/frontend-foundation/ui";
import { operatorMobileErrorMessage } from "../../core/operator-mobile-error.util";
import { NativeCapabilityService } from "../../core/native-capability.service";

@Component({ standalone: true, imports: [FormsModule, HisHopeTranslatePipe, HisHopeSelectComponent], templateUrl: "./maintenance-work-page.component.html", styleUrls: ["./maintenance-work-page.component.scss"] })
export class MaintenanceWorkPageComponent {
  private readonly api = inject(OperatorMobileApiService);
  private readonly queue = inject(OperationQueueService);
  private readonly tenant = inject(OperatorMobileTenantContextService);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly native = inject(NativeCapabilityService);
  checklistComplete = false;
  checklistItems: Array<{ label: string; complete: boolean }> = [];
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
  downtimes: MachineDowntime[] = [];
  downtimeReason = "";

  maintenanceStatusLabel(status: string): string { return manufacturingEnumLabel(this.i18n, "maintenanceStatus", status); }
  maintenanceTypeLabel(type: string): string { return manufacturingEnumLabel(this.i18n, "maintenanceType", type); }
  machineStateLabel(state: string): string { return manufacturingEnumLabel(this.i18n, "machineState", state); }
  calibrationResultLabel(result: string): string { return manufacturingEnumLabel(this.i18n, "qualityTestResult", result); }
  calibrationTypeLabel(type: string): string { return manufacturingEnumLabel(this.i18n, "calibrationTypes", type); }
  calibrationDueLabel(value: string): string { return this.i18n.formatDate(value, { dateStyle: "medium" }); }
  telemetryObservedLabel(value: string): string { return this.i18n.formatDateTime(value); }
  formatNumber(value: number): string { return this.i18n.formatNumber(value); }
  hasOpenDowntime(): boolean { return this.downtimes.some((item) => item.status === "Open"); }

  loadWorkOrderChecklist(): void {
    const order = this.workOrders.find((item) => item.id === this.workOrderId);
    if (!order) {
      this.checklistItems = [];
      this.checklistComplete = false;
      return;
    }
    const source = order.notes?.trim() || "Isolation checklist";
    this.checklistItems = source.split(/[;\n]+/).map((item) => item.trim()).filter(Boolean).map((label) => ({ label, complete: false }));
    this.checklistComplete = false;
  }

  updateChecklistItem(index: number, complete: boolean): void {
    const item = this.checklistItems[index];
    if (!item) return;
    item.complete = complete;
    this.checklistComplete = this.checklistItems.every((entry) => entry.complete);
  }

  async captureEvidence(): Promise<void> {
    const photo = await this.native.capturePhoto({ quality: 82, width: 1600, height: 1600 });
    if (photo?.uri) this.evidence = photo.uri;
  }

  loadCalibrations(): void {
    const machineId = this.machineId.trim();
    if (!machineId) { this.calibrations = []; this.telemetry = []; return; }
    this.api.getMachineCalibrations(machineId).pipe(catchError((error) => { this.loadError = operatorMobileErrorMessage(this.i18n, error); this.cdr.markForCheck(); return of([]); })).subscribe((calibrations) => {
      setTimeout(() => { this.calibrations = calibrations; this.cdr.markForCheck(); });
    });
    this.api.getMachineTelemetry(machineId).pipe(catchError((error) => { this.loadError = operatorMobileErrorMessage(this.i18n, error); this.cdr.markForCheck(); return of([]); })).subscribe((telemetry) => {
      setTimeout(() => { this.telemetry = telemetry; this.cdr.markForCheck(); });
    });
    this.api.getMachineDowntimes(machineId).pipe(catchError(() => of([]))).subscribe((downtimes) => {
      setTimeout(() => { this.downtimes = downtimes; this.cdr.markForCheck(); });
    });
  }

  constructor() {
    effect(() => {
      const tenantKey = this.tenant.activeTenantKey?.();
      if (!tenantKey) { this.machines = []; this.workOrders = []; this.plans = []; this.calibrations = []; this.telemetry = []; this.downtimes = []; this.health = null; return; }
      this.api.getMachineHealth().pipe(catchError((error) => { this.loadError = operatorMobileErrorMessage(this.i18n, error); this.cdr.markForCheck(); return of(null); })).subscribe((health) => {
        setTimeout(() => { this.health = health; this.cdr.markForCheck(); });
      });
      this.api.getMachines("Available").pipe(catchError((error) => { this.loadError = operatorMobileErrorMessage(this.i18n, error); this.cdr.markForCheck(); return of([]); })).subscribe((machines) => {
        setTimeout(() => {
          this.machines = machines;
          if (this.machineId && !machines.some((machine) => machine.id === this.machineId)) this.machineId = "";
          this.loadCalibrations();
          this.cdr.markForCheck();
        });
      });
      this.api.getMaintenanceWorkOrders("Open").pipe(catchError((error) => { this.loadError = operatorMobileErrorMessage(this.i18n, error); this.cdr.markForCheck(); return of([]); })).subscribe((orders) => {
        setTimeout(() => {
          this.workOrders = orders;
          if (this.workOrderId && !orders.some((order) => order.id === this.workOrderId)) this.workOrderId = "";
          if (this.workOrderId) this.loadWorkOrderChecklist();
          this.cdr.markForCheck();
        });
      });
      this.api.getMaintenancePlans().pipe(catchError((error) => { this.loadError = operatorMobileErrorMessage(this.i18n, error); this.cdr.markForCheck(); return of([]); })).subscribe((plans) => {
        setTimeout(() => { this.plans = plans; this.cdr.markForCheck(); });
      });
    });
  }

  async completeWorkOrder(): Promise<void> {
    if (!this.checklistComplete || (this.checklistItems.length > 0 && !this.checklistItems.every((item) => item.complete))) {
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

  async recordDowntime(): Promise<void> {
    const scope = this.tenant.commandScope;
    if (!scope || !this.machineId.trim() || !this.downtimeReason.trim()) {
      this.message = this.i18n.t("mobile.operatorDowntimeValidation", "Select a machine and enter a downtime reason.");
      return;
    }
    const operation = await this.queue.submit(
      { ...scope, endpoint: `/machines/${this.machineId}/downtimes`, payload: { reason: this.downtimeReason.trim(), startedAt: new Date().toISOString() } },
      (queued) => this.api.recordMachineDowntime(queued),
    );
    this.message = operation.status === "synced" ? this.i18n.t("mobile.operatorDowntimeRecorded", "Downtime recorded.") : this.i18n.t("mobile.operatorPendingSync", "Pending sync — it will retry when connected.");
    if (operation.status === "synced") this.loadCalibrations();
  }

  async resolveDowntime(): Promise<void> {
    const scope = this.tenant.commandScope;
    const downtime = this.downtimes.find((item) => item.status === "Open");
    if (!scope || !this.machineId.trim() || !downtime) {
      this.message = this.i18n.t("mobile.operatorNoOpenDowntime", "No open downtime is available for this machine.");
      return;
    }
    const operation = await this.queue.submit(
      { ...scope, endpoint: `/machines/${this.machineId}/downtimes/${downtime.id}/resolve`, payload: { endedAt: new Date().toISOString() } },
      (queued) => this.api.resolveMachineDowntime(queued),
    );
    this.message = operation.status === "synced" ? this.i18n.t("mobile.operatorDowntimeResolved", "Downtime resolved.") : this.i18n.t("mobile.operatorPendingSync", "Pending sync — it will retry when connected.");
    if (operation.status === "synced") this.loadCalibrations();
  }
}
