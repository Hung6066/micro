import { ChangeDetectionStrategy, ChangeDetectorRef, Component, DestroyRef, inject, input } from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { catchError, finalize, of, type Observable } from "rxjs";
import type { HisHopeEntityStatusHistoryDto } from "@his-hope/frontend-foundation/contracts";
import { HisHopeTimelineComponent } from "@his-hope/frontend-foundation/domain";
import { HisHopeActionButtonComponent } from "@his-hope/frontend-foundation/ui";
import { HisHopeTranslatePipe, HisHopeI18nService } from "@his-hope/frontend-foundation/i18n";
import { mapEntityStatusHistoryToTimeline } from "../utils/entity-status-history.util";

@Component({
  selector: "app-entity-status-history-panel",
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [HisHopeTimelineComponent, HisHopeActionButtonComponent, HisHopeTranslatePipe],
  template: `
    <div class="history-panel" [attr.data-testid]="'entity-status-history-' + entityId()">
      <hh-action-button
        kind="row"
        icon="history"
        [attr.data-testid]="'entity-status-history-toggle-' + entityId()"
        [label]="
          expanded
            ? ('customerPortal.hideStatusHistory' | hhTranslate: 'Hide status history')
            : ('customerPortal.showStatusHistory' | hhTranslate: 'Show status history')
        "
        [disabled]="loading"
        (pressed)="toggle()"
      />
      @if (expanded) {
        @if (loading) {
          <p class="meta">{{ "customerPortal.loadingStatusHistory" | hhTranslate: "Loading status history…" }}</p>
        } @else if (error) {
          <p class="error" role="alert">{{ error }}</p>
        } @else if (timelineItems.length) {
          <hh-timeline [items]="timelineItems" />
        } @else {
          <p class="meta">{{ "customerPortal.noStatusHistory" | hhTranslate: "No status history recorded." }}</p>
        }
      }
    </div>
  `,
  styles: [
    `
      :host {
        display: block;
      }
      .history-panel {
        margin-top: var(--space-sm);
      }
      .meta {
        margin: var(--space-xs) 0 0;
        color: var(--text-secondary);
        font-size: var(--font-size-caption);
      }
      .error {
        margin: var(--space-xs) 0 0;
        color: var(--color-danger);
        font-size: var(--font-size-caption);
      }
    `,
  ],
})
export class EntityStatusHistoryPanelComponent {
  readonly entityId = input.required<string>();
  readonly loadHistory = input.required<(entityId: string) => Observable<HisHopeEntityStatusHistoryDto[]>>();
  readonly statusLabel = input.required<(status: string) => string>();

  private readonly destroyRef = inject(DestroyRef);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly i18n = inject(HisHopeI18nService);

  expanded = false;
  loading = false;
  error = "";
  timelineItems: ReturnType<typeof mapEntityStatusHistoryToTimeline> = [];
  private loaded = false;

  toggle(): void {
    this.expanded = !this.expanded;
    if (this.expanded && !this.loaded) {
      this.fetch();
    } else {
      this.cdr.markForCheck();
    }
  }

  private fetch(): void {
    this.loading = true;
    this.error = "";
    this.loadHistory()(this.entityId())
      .pipe(
        catchError(() => {
          this.error = this.i18n.t(
            "customerPortal.statusHistoryLoadFailed",
            "Unable to load status history.",
          );
          return of([] as HisHopeEntityStatusHistoryDto[]);
        }),
        finalize(() => {
          this.loading = false;
          this.loaded = true;
          this.cdr.markForCheck();
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((history) => {
        this.timelineItems = mapEntityStatusHistoryToTimeline(
          history ?? [],
          this.statusLabel(),
          this.i18n,
        );
        this.cdr.markForCheck();
      });
  }
}
