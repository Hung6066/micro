import { Component, inject } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { OperationQueueService } from "../../core/offline/operation-queue.service";
import { OperatorMobileApiService } from "../../core/services/operator-mobile-api.service";
import { OperatorMobileTenantContextService } from "../../core/operator-mobile-tenant-context.service";

@Component({ standalone: true, imports: [FormsModule], templateUrl: "./maintenance-work-page.component.html", styleUrls: ["./maintenance-work-page.component.scss"] })
export class MaintenanceWorkPageComponent {
  private readonly api = inject(OperatorMobileApiService);
  private readonly queue = inject(OperationQueueService);
  private readonly tenant = inject(OperatorMobileTenantContextService);
  checklistComplete = false;
  machineId = "";
  workOrderId = "";
  technician = "";
  message = "";

  async completeWorkOrder(): Promise<void> {
    if (!this.checklistComplete) {
      this.message = "Complete the checklist before closing the work order.";
      return;
    }
    if (!this.machineId.trim() || !this.workOrderId.trim() || !this.technician.trim()) {
      this.message = "Machine, work order and technician are required.";
      return;
    }
    const scope = this.tenant.commandScope;
    if (!scope) {
      this.message = "Sign in and select an operational tenant before completing maintenance.";
      return;
    }
    const operation = await this.queue.submit(
      { ...scope, endpoint: `/machines/${this.machineId}/maintenance-work-orders/${this.workOrderId}/complete`, payload: { technician: this.technician.trim(), completedAt: new Date().toISOString(), tenantKey: scope.tenantKey } },
      (queued) => this.api.completeMaintenanceWorkOrder(queued),
    );
    this.message = operation.status === "synced" ? "Work order completed." : "Completion pending sync.";
  }
}
