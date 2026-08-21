import {
  ChangeDetectionStrategy,
  Component,
  input,
  output,
} from "@angular/core";
import {
  HisHopeBulkAction,
  HisHopeBulkActionRequest,
  HisHopePageQuery,
  HisHopeTableExportRequest,
} from "@his-hope/frontend-foundation/contracts";
import { HisHopeTranslatePipe } from "@his-hope/frontend-foundation/i18n";
import {
  HisHopeDataTableColumn,
  HisHopeDataTableComponent,
  HisHopeDataTableDetailDirective,
} from "./his-hope-data-table.component";
import { HisHopeMobileActionSheetComponent } from "./his-hope-mobile-components";
import { HisHopeMobileBottomSheetComponent } from "./his-hope-mobile-components";
import { HisHopeMobileIconComponent } from "./his-hope-mobile-icon.component";
import { HisHopeMobileInfiniteListComponent } from "./his-hope-mobile-components";
import { HisHopeMobileRefresherComponent } from "./his-hope-mobile-components";
import { HisHopeMobileSearchbarComponent } from "./his-hope-mobile-components";
import { HisHopeToolbarComponent } from "./his-hope-toolbar.component";

/**
 * Mobile composition shell for server-backed admin resource lists: sticky toolbar,
 * search, pull-to-refresh, infinite scroll, and list-mode data table wiring.
 */
@Component({
  selector: "hh-mobile-resource-list-page",
  standalone: true,
  imports: [
    HisHopeDataTableComponent,
    HisHopeDataTableDetailDirective,
    HisHopeMobileActionSheetComponent,
    HisHopeMobileBottomSheetComponent,
    HisHopeMobileIconComponent,
    HisHopeMobileInfiniteListComponent,
    HisHopeMobileRefresherComponent,
    HisHopeMobileSearchbarComponent,
    HisHopeToolbarComponent,
    HisHopeTranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="hh-mobile-resource-page" [attr.aria-busy]="loading()">
      <hh-toolbar class="hh-mobile-resource-page__toolbar" [label]="toolbarLabel()">
        <span hhToolbarTitle class="hh-mobile-resource-page__count">
          <span class="hh-mobile-resource-page__count-value">{{
            totalItems()
          }}</span>
          <span class="hh-mobile-resource-page__count-label">{{
            countLabel() | hhTranslate: countLabelFallback()
          }}</span>
        </span>
        @if (canWrite() && showCreate()) {
          <button
            hh-toolbar-actions
            class="hh-icon-button"
            type="button"
            [attr.aria-label]="createLabel() | hhTranslate: createLabelFallback()"
            [attr.title]="createLabel() | hhTranslate: createLabelFallback()"
            (click)="create.emit()"
          >
            <hh-mobile-icon name="add" />
          </button>
        }
        <button
          hh-toolbar-actions
          class="hh-icon-button hh-mobile-resource-page__refresh"
          type="button"
          (click)="refresh.emit()"
          [disabled]="loading()"
          [attr.aria-label]="refreshLabel() | hhTranslate: refreshLabelFallback()"
          [attr.title]="refreshLabel() | hhTranslate: refreshLabelFallback()"
        >
          <hh-mobile-icon
            name="refresh"
            [class.hh-mobile-resource-page__refresh--spinning]="loading()"
          />
        </button>
        <button
          hh-toolbar-actions
          class="hh-icon-button"
          type="button"
          [attr.aria-label]="moreActionsLabel() | hhTranslate: moreActionsLabelFallback()"
          [attr.title]="moreActionsLabel() | hhTranslate: moreActionsLabelFallback()"
          (click)="actionsOpen = true"
        >
          <hh-mobile-icon name="more" />
        </button>
      </hh-toolbar>

      <hh-mobile-searchbar
        [value]="searchValue()"
        [placeholder]="searchPlaceholder() | hhTranslate: searchPlaceholderFallback()"
        (valueChange)="searchChange.emit($event)"
      />

      <hh-mobile-refresher (refreshed)="refresh.emit()">
        <hh-mobile-infinite-list
          [label]="tableLabel() | hhTranslate: tableLabelFallback()"
          [loading]="loadingMore()"
          [hasMore]="hasMore()"
          [loadedCount]="rows().length"
          [totalCount]="totalItems()"
          [nextCursor]="nextCursor() || ''"
          (loadMoreRequested)="loadMore.emit($event.cursor)"
        >
          <hh-data-table
            [label]="tableLabel() | hhTranslate: tableLabelFallback()"
            [columns]="columns()"
            [rows]="rows()"
            [loading]="loading()"
            [error]="error()"
            [empty]="!loading() && !error() && rows().length === 0"
            [emptyMessage]="emptyMessage() | hhTranslate: emptyMessageFallback()"
            [selection]="selection()"
            [mobilePresentation]="'list'"
            [rowClickable]="true"
            [searchable]="false"
            mode="server"
            [totalItems]="totalItems()"
            [query]="query()"
            [pageSize]="query().pageSize || 20"
            [urlSync]="false"
            [bulkActions]="bulkActions()"
            [filterBuilder]="filterBuilder()"
            [mobileLoadMore]="false"
            (queryChange)="queryChange.emit($event)"
            (rowClick)="rowClick.emit($event)"
            (bulkActionRequested)="bulkAction.emit($event)"
            (exportRequested)="exportRequested.emit($event)"
            (retry)="refresh.emit()"
          >
            <ng-content select="[hhDataTableDetail]" />
          </hh-data-table>
        </hh-mobile-infinite-list>
      </hh-mobile-refresher>

      <hh-mobile-action-sheet
        [open]="actionsOpen"
        [label]="actionSheetLabel() | hhTranslate: actionSheetLabelFallback()"
        (close)="actionsOpen = false"
      >
        <button
          type="button"
          class="hh-mobile-sheet-action"
          (click)="actionsOpen = false; refresh.emit()"
        >
          <hh-mobile-icon name="refresh" />
          {{ refreshListLabel() | hhTranslate: refreshListLabelFallback() }}
        </button>
        <ng-content select="[hhMobileResourceActions]" />
      </hh-mobile-action-sheet>

      <hh-mobile-bottom-sheet
        [open]="detailOpen()"
        [label]="detailLabel() | hhTranslate: detailLabelFallback()"
        (close)="detailClose.emit()"
      >
        <ng-content select="[hhMobileResourceDetailSheet]" />
      </hh-mobile-bottom-sheet>
    </section>
  `,
  styles: [
    `
      :host {
        display: block;
      }
      .hh-mobile-resource-page {
        display: grid;
        gap: var(--space-md);
      }
      :host ::ng-deep .hh-mobile-resource-page__toolbar .hh-toolbar {
        display: grid;
        grid-template-columns: minmax(0, 1fr) auto;
        grid-template-areas: "title actions";
        align-items: center;
        column-gap: var(--space-sm);
        margin-bottom: 0;
      }
      :host ::ng-deep .hh-mobile-resource-page__toolbar .hh-toolbar__title {
        grid-area: title;
        display: block;
        min-width: 0;
        overflow: hidden;
      }
      :host ::ng-deep .hh-mobile-resource-page__toolbar .hh-toolbar__leading,
      :host ::ng-deep .hh-mobile-resource-page__toolbar .hh-toolbar__controls {
        display: none;
      }
      .hh-mobile-resource-page__count {
        display: block;
        overflow: hidden;
        text-overflow: ellipsis;
        white-space: nowrap;
        line-height: 1.25;
      }
      .hh-mobile-resource-page__count-label {
        margin-inline-start: var(--space-xs);
        color: var(--text-secondary);
        font-size: var(--font-size-caption);
        font-weight: var(--font-weight-semibold);
      }
      .hh-mobile-resource-page__count-value {
        font-size: var(--font-size-section);
        font-weight: var(--font-weight-semibold);
        font-variant-numeric: tabular-nums;
      }
      :host ::ng-deep .hh-mobile-resource-page__toolbar .hh-toolbar__actions {
        grid-area: actions;
        flex: 0 0 auto;
        width: auto;
        margin-left: 0;
        flex-shrink: 0;
      }
      .hh-mobile-resource-page__toolbar {
        position: sticky;
        top: calc(var(--mobile-toolbar-height) + env(safe-area-inset-top));
        z-index: 5;
        min-width: 0;
        padding-block: var(--space-2xs);
        background: color-mix(in srgb, var(--bg-warm) 94%, transparent);
        backdrop-filter: blur(var(--blur-toolbar));
      }
      .hh-mobile-resource-page__refresh {
        flex: 0 0 var(--control-height-touch);
        width: var(--control-height-touch);
        min-width: var(--control-height-touch);
        min-height: var(--control-height-touch);
        padding: 0;
      }
      .hh-mobile-resource-page__refresh:disabled {
        opacity: 0.55;
        cursor: wait;
      }
      .hh-mobile-resource-page__refresh--spinning {
        animation: hh-mobile-resource-refresh-spin 0.8s linear infinite;
      }
      .hh-mobile-sheet-action {
        display: flex;
        align-items: center;
        gap: var(--space-md);
        min-height: var(--menu-item-height);
        padding: 0 var(--space-sm);
        border: 0;
        border-bottom: 1px solid var(--border-light);
        background: transparent;
        color: var(--text-primary);
        font: inherit;
        text-align: left;
      }
      @keyframes hh-mobile-resource-refresh-spin {
        to {
          transform: rotate(360deg);
        }
      }
    `,
  ],
})
export class HisHopeMobileResourceListPageComponent {
  actionsOpen = false;

  readonly toolbarLabel = input("");
  readonly countLabel = input.required<string>();
  readonly countLabelFallback = input("");
  readonly totalItems = input(0);
  readonly canWrite = input(false);
  readonly showCreate = input(true);
  readonly createLabel = input("admin.create");
  readonly createLabelFallback = input("");
  readonly refreshLabel = input("admin.refresh");
  readonly refreshLabelFallback = input("");
  readonly moreActionsLabel = input("mobile.moreActions");
  readonly moreActionsLabelFallback = input("");
  readonly refreshListLabel = input("mobile.refreshList");
  readonly refreshListLabelFallback = input("");
  readonly searchValue = input("");
  readonly searchPlaceholder = input("common.search");
  readonly searchPlaceholderFallback = input("");
  readonly tableLabel = input.required<string>();
  readonly tableLabelFallback = input("");
  readonly actionSheetLabel = input("");
  readonly actionSheetLabelFallback = input("");
  readonly detailLabel = input("mobile.details");
  readonly detailLabelFallback = input("");
  readonly columns = input.required<HisHopeDataTableColumn[]>();
  readonly rows = input.required<Record<string, unknown>[]>();
  readonly loading = input(false);
  readonly loadingMore = input(false);
  readonly error = input("");
  readonly emptyMessage = input("mobile.noRecords");
  readonly emptyMessageFallback = input("");
  readonly selection = input(true);
  readonly filterBuilder = input(true);
  readonly bulkActions = input<HisHopeBulkAction[]>([]);
  readonly query = input.required<HisHopePageQuery>();
  readonly hasMore = input(false);
  readonly nextCursor = input<string | null>(null);
  readonly detailOpen = input(false);

  readonly create = output<void>();
  readonly refresh = output<void>();
  readonly searchChange = output<string>();
  readonly loadMore = output<string | null>();
  readonly queryChange = output<HisHopePageQuery>();
  readonly rowClick = output<Record<string, unknown>>();
  readonly bulkAction = output<HisHopeBulkActionRequest>();
  readonly exportRequested = output<HisHopeTableExportRequest>();
  readonly detailClose = output<void>();
}
