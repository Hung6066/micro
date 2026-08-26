import { Component, effect, inject } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { OperationQueueService } from "../../core/offline/operation-queue.service";
import { OperatorMobileApiService, type Machine, type MaintenanceWorkOrder } from "../../core/services/operator-mobile-api.service";
import { catchError, of } from "rxjs";
import { OperatorMobileTenantContextService } from "../../core/operator-mobile-tenant-context.service";
import { HisHopeI18nService, HisHopeTranslatePipe } from "@his-hope/frontend-foundation/i18n";

@Component({ standalone: true, imports: [FormsModule, HisHopeTranslatePipe], templateUrl: "./maintenance-work-page.component.html", styleUrls: ["./maintenance-work-page.component.scss"] })
export class MaintenanceWorkPageComponent {
  private readonly api = inject(OperatorMobileApiService);
  private readonly queue = inject(OperationQueueService);
  private readonly tenant = inject(OperatorMobileTenantContextService);
  private readonly i18n = inject(HisHopeI18nService);
  checklistComplete = false;
  machineId = "";
  workOrderId = "";
  technician = "";
  message = "";
  machines: Machine[] = [];
  workOrders: MaintenanceWorkOrder[] = [];

  constructor() {
    effect(() => {
      const tenantKey = this.tenant.activeTenantKey?.();
      if (!tenantKey) { this.machines = []; this.workOrders = []; return; }
      this.api.getMachines("Available").pipe(catchError(() => of([]))).subscribe((machines) => {
        this.machines = machines;
        if (this.machineId && !machines.some((machine) => machine.id === this.machineId)) this.machineId = "";
      });
      this.api.getMaintenanceWorkOrders("Open").pipe(catchError(() => of([]))).subscribe((orders) => {
        this.workOrders = orders;
        if (this.workOrderId && !orders.some((order) => order.id === this.workOrderId)) this.workOrderId = "";
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
      { ...scope, endpoint: `/machines/${this.machineId}/maintenance-work-orders/${this.workOrderId}/complete`, payload: { technician: this.technician.trim(), completedAt: new Date().toISOString(), tenantKey: scope.tenantKey } },
      (queued) => this.api.completeMaintenanceWorkOrder(queued),
    );
    this.message = operation.status === "synced" ? "Work order completed." : "Completion pending sync.";
  }
}
