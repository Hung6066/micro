import { Component, effect, inject } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { OperationQueueService } from "../../core/offline/operation-queue.service";
import { OperatorMobileApiService } from "../../core/services/operator-mobile-api.service";
import { OperatorMobileTenantContextService } from "../../core/operator-mobile-tenant-context.service";
import { HisHopeI18nService, HisHopeTranslatePipe } from "@his-hope/frontend-foundation/i18n";
import { catchError, of } from "rxjs";
import type { LotSummary } from "../../core/services/operator-mobile-api.service";

@Component({ standalone: true, imports: [FormsModule, HisHopeTranslatePipe], templateUrl: "./quality-inspection-page.component.html", styleUrls: ["./quality-inspection-page.component.scss"] })
export class QualityInspectionPageComponent {
  private readonly api = inject(OperatorMobileApiService);
  private readonly queue = inject(OperationQueueService);
  private readonly tenant = inject(OperatorMobileTenantContextService);
  private readonly i18n = inject(HisHopeI18nService);
  lotId = "";
  inspector = "";
  moisturePercent = 0;
  status = "Pass";
  message = "";
  lots: LotSummary[] = [];

  constructor() {
    effect(() => {
      const tenantKey = this.tenant.activeTenantKey?.();
      if (!tenantKey) { this.lots = []; return; }
      this.api.getLots().pipe(catchError(() => of([]))).subscribe((lots) => {
        this.lots = lots;
        if (this.lotId && !lots.some((lot) => lot.id === this.lotId)) this.lotId = "";
      });
    });
  }

  async submitInspection(): Promise<void> {
    if (!this.lotId.trim() || !this.inspector.trim()) {
      this.message = this.i18n.t("mobile.operatorQualityValidation", "Lot and inspector are required.");
      return;
    }
    const scope = this.tenant.commandScope;
    if (!scope) {
      this.message = this.i18n.t("mobile.operatorTenantRequired", "Sign in and select an operational tenant before saving an inspection.");
      return;
    }
    const operation = await this.queue.submit(
      { ...scope, endpoint: "/quality-inspections", payload: { lotId: this.lotId.trim(), tenantKey: scope.tenantKey, status: this.status, moisturePercent: this.moisturePercent, inspector: this.inspector.trim() } },
      (queued) => this.api.createQualityInspection(queued),
    );
    this.message = operation.status === "synced" ? "Inspection saved." : "Inspection pending sync.";
  }
}
