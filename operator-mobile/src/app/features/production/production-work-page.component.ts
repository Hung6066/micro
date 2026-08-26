import { Component, OnInit, inject } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { catchError, of } from "rxjs";
import { OperationQueueService } from "../../core/offline/operation-queue.service";
import { OperatorMobileApiService, type ProductionBatch } from "../../core/services/operator-mobile-api.service";
import { OperatorMobileTenantContextService } from "../../core/operator-mobile-tenant-context.service";

@Component({ standalone: true, imports: [FormsModule], templateUrl: "./production-work-page.component.html", styleUrls: ["./production-work-page.component.scss"] })
export class ProductionWorkPageComponent implements OnInit {
  private readonly api = inject(OperatorMobileApiService);
  private readonly queue = inject(OperationQueueService);
  private readonly tenant = inject(OperatorMobileTenantContextService);
  batches: ProductionBatch[] = [];
  selectedBatch: ProductionBatch | null = null;
  outputQuantity = 0;
  message = "";

  ngOnInit(): void {
    this.api.getProductionBatches("Started").pipe(catchError(() => of([]))).subscribe((batches) => (this.batches = batches));
  }

  async recordOperation(): Promise<void> {
    if (!this.selectedBatch || this.outputQuantity <= 0) {
      this.message = "Select a batch and enter a positive output quantity.";
      return;
    }
    const scope = this.tenant.commandScope;
    if (!scope) {
      this.message = "Sign in and select an operational tenant before recording work.";
      return;
    }
    const operation = await this.queue.submit(
      { ...scope, endpoint: `/production-batches/${this.selectedBatch.id}/operations`, expectedVersion: this.selectedBatch.version, payload: { sequence: 1, processStep: "operation", operator: scope.subjectId, inputQuantity: this.outputQuantity, outputQuantity: this.outputQuantity, required: true, tenantKey: scope.tenantKey } },
      (queued) => this.api.recordProductionOperation(queued),
    );
    this.message = operation.status === "synced" ? "Operation recorded." : "Pending sync — it will retry when connected.";
  }
}
