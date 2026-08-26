import { Component, OnInit, inject } from "@angular/core";
import { OperationQueueService } from "../../core/offline/operation-queue.service";
import type { QueuedOperation } from "../../core/offline/operation-queue.models";
import { HisHopeTranslatePipe } from "@his-hope/frontend-foundation/i18n";

@Component({ standalone: true, imports: [HisHopeTranslatePipe], templateUrl: "./sync-page.component.html", styleUrls: ["./sync-page.component.scss"] })
export class SyncPageComponent implements OnInit {
  private readonly queue = inject(OperationQueueService);
  entries: QueuedOperation[] = [];

  ngOnInit(): void { void this.refresh(); }
  async refresh(): Promise<void> { this.entries = await this.queue.entries(); }
  async discard(entry: QueuedOperation): Promise<void> { await this.queue.discard(entry.id); await this.refresh(); }
}
