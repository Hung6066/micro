import { Component, inject } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { OperationQueueService } from "../../core/offline/operation-queue.service";
import { OperatorMobileApiService } from "../../core/services/operator-mobile-api.service";
import { OperatorMobileTenantContextService } from "../../core/operator-mobile-tenant-context.service";

@Component({ standalone: true, imports: [FormsModule], templateUrl: "./quality-inspection-page.component.html", styleUrls: ["./quality-inspection-page.component.scss"] })
export class QualityInspectionPageComponent {
  private readonly api = inject(OperatorMobileApiService);
  private readonly queue = inject(OperationQueueService);
  private readonly tenant = inject(OperatorMobileTenantContextService);
  lotId = "";
  inspector = "";
  moisturePercent = 0;
  status = "Pass";
  message = "";

  async submitInspection(): Promise<void> {
    if (!this.lotId.trim() || !this.inspector.trim()) {
      this.message = "Lot and inspector are required.";
      return;
    }
    const scope = this.tenant.commandScope;
    if (!scope) {
      this.message = "Sign in and select an operational tenant before saving an inspection.";
      return;
    }
    const operation = await this.queue.submit(
      { ...scope, endpoint: "/quality-inspections", payload: { lotId: this.lotId.trim(), tenantKey: scope.tenantKey, status: this.status, moisturePercent: this.moisturePercent, inspector: this.inspector.trim() } },
      (queued) => this.api.createQualityInspection(queued),
    );
    this.message = operation.status === "synced" ? "Inspection saved." : "Inspection pending sync.";
  }
}
