import { Component, OnInit, inject } from "@angular/core";
import { OperationQueueService } from "../../core/offline/operation-queue.service";
import type { QueuedOperation } from "../../core/offline/operation-queue.models";
import { OperatorMobileApiService } from "../../core/services/operator-mobile-api.service";
import { HisHopeI18nService, HisHopeTranslatePipe } from "@his-hope/frontend-foundation/i18n";

@Component({ standalone: true, imports: [HisHopeTranslatePipe], templateUrl: "./sync-page.component.html", styleUrls: ["./sync-page.component.scss"] })
export class SyncPageComponent implements OnInit {
  private readonly queue = inject(OperationQueueService);
  private readonly api = inject(OperatorMobileApiService);
  private readonly i18n = inject(HisHopeI18nService);
  entries: QueuedOperation[] = [];
  syncing = false;
  message = "";

  statusLabel(status: QueuedOperation["status"]): string { return this.i18n.t(`mobile.operatorQueueStatus${status[0].toUpperCase()}${status.slice(1)}`, status); }

  ngOnInit(): void { void this.refresh(); }
  async refresh(): Promise<void> { this.entries = await this.queue.entries(); }
  async sync(): Promise<void> {
    this.syncing = true;
    await this.queue.sync((operation) => this.api.syncOperation(operation));
    this.syncing = false;
    this.message = this.i18n.t("mobile.operatorSyncCompleted", "Sync attempt completed.");
    await this.refresh();
  }
  async retry(entry: QueuedOperation): Promise<void> { await this.queue.retry(entry.id); await this.sync(); }
  async discard(entry: QueuedOperation): Promise<void> { await this.queue.discard(entry.id); await this.refresh(); }
}
