import { ActivatedRoute, Router } from '@angular/router';
import { ChangeDetectionStrategy, Component, ContentChildren, Directive, DestroyRef, EventEmitter, QueryList, TemplateRef, Output, effect, inject, input, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { HostListener } from '@angular/core';
import { HisHopeBulkAction, HisHopeBulkActionRequest, HisHopeBulkJob, HisHopeConflictResolution, HisHopeFilterGroup, HisHopeFilterOperator, HisHopePageQuery, HisHopeSort, HisHopeTableAnalysisRequest, HisHopeTableEditConflict, HisHopeTableEditSaveRequest, HisHopeTableEditState, HisHopeTableEditor, HisHopeTableEditorOption, HisHopeTableExportFormat, HisHopeTableExportRequest, HisHopeTableImportRequest, HisHopeTableSelectionScope, HisHopeTableSelectionState } from '../contracts/his-hope-ui-contracts';
import { HisHopeTableEditorComponent } from './his-hope-table-editor.component';
import { HisHopeStatusBadgeComponent } from './his-hope-status-badge.component';

export type HisHopeDataTableDensity = 'comfortable' | 'compact';
export type HisHopeDataTableSortDirection = 'asc' | 'desc';
export type HisHopeDataTableMobilePresentation = 'auto' | 'table' | 'list';
export interface HisHopeDataTableColumn {
  key: string;
  label: string;
  sortable?: boolean;
  editable?: boolean;
  editor?: HisHopeTableEditor;
  options?: HisHopeTableEditorOption[];
  editValidator?: (value: unknown, row: Record<string, unknown>) => string | null;
  width?: string;
  minWidth?: number;
  maxWidth?: number;
  /** 1 stays visible, 2 hides on mobile, 3 hides on tablet and mobile. */
  responsivePriority?: 1 | 2 | 3;
  reorderable?: boolean;
  hideable?: boolean;
  align?: 'start' | 'center' | 'end';
  pinned?: 'left' | 'right';
  sensitive?: boolean;
  exportable?: boolean;
  status?: boolean;
  computed?: (row: Record<string, unknown>) => unknown;
}

@Directive({ selector: '[hhDataTableDetail]', standalone: true })
export class HisHopeDataTableDetailDirective {
  constructor(readonly template: TemplateRef<unknown>) {}
}
export interface HisHopeDataTableSort {
  key: string;
  direction: HisHopeDataTableSortDirection;
}
export type HisHopeDataTableMode = 'client' | 'server';
export type HisHopeDataTableUrlParams = Record<string, string | null>;

export function serializeHisHopeDataTableQuery(query: HisHopePageQuery): HisHopeDataTableUrlParams {
  return {
    page: String(query.page || 1),
    pageSize: String(query.pageSize || 20),
    cursor: query.cursor ?? null,
    search: query.search?.trim() || null,
    sort: query.sort ? JSON.stringify(query.sort) : null,
    filters: query.filters ? JSON.stringify(query.filters) : null,
    filterItems: query.filterItems ? JSON.stringify(query.filterItems) : null,
  };
}

export function parseHisHopeDataTableQuery(params: URLSearchParams | Record<string, string | null | undefined>): HisHopePageQuery {
  const read = (key: string): string | undefined => params instanceof URLSearchParams ? params.get(key) ?? undefined : params[key] ?? undefined;
  const parseJson = <T>(key: string): T | undefined => {
    const value = read(key);
    if (!value) return undefined;
    try { return JSON.parse(value) as T; } catch { return undefined; }
  };
  const page = Number(read('page'));
  const pageSize = Number(read('pageSize'));
  return {
    page: Number.isFinite(page) && page > 0 ? page : 1,
    pageSize: Number.isFinite(pageSize) && pageSize > 0 ? pageSize : 20,
    cursor: read('cursor'),
    search: read('search')?.trim() || undefined,
    sort: parseJson<HisHopeSort>('sort'),
    filters: parseJson<HisHopePageQuery['filters']>('filters'),
    filterItems: parseJson<HisHopePageQuery['filterItems']>('filterItems'),
  };
}

export function toggleHisHopeDataTableSort(current: HisHopeDataTableSort[], key: string, append: boolean): HisHopeDataTableSort[] {
  const existing = current.find(item => item.key === key);
  const nextTerm: HisHopeDataTableSort = { key, direction: existing?.direction === 'asc' ? 'desc' : 'asc' };
  return append ? [...current.filter(item => item.key !== key), nextTerm] : [nextTerm];
}

export function createHisHopeCursorQuery(query: HisHopePageQuery, cursor: string | null): HisHopePageQuery {
  return { ...query, page: 1, cursor: cursor || undefined };
}

@Directive({ selector: 'ng-template[hhDataTableCell]', standalone: true })
export class HisHopeDataTableCellDirective {
  constructor(readonly template: TemplateRef<unknown>) {}
  key = input.required<string>({ alias: 'hhDataTableCell' });
}

@Component({
  selector: 'hh-data-table',
  standalone: true,
  imports: [CommonModule, HisHopeTableEditorComponent, HisHopeStatusBadgeComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="hh-data-table" [class.hh-data-table--compact]="density() === 'compact'"
             [class.hh-data-table--mobile-list]="mobilePresentation() !== 'table'"
             [class.hh-data-table--sticky-header]="stickyHeader()" [class.hh-data-table--sticky-footer]="stickyFooter()"
             [attr.aria-label]="label()" [attr.aria-busy]="loading()">
      @if (loading() && !rows().length) {
        <div class="hh-data-table__state" role="status" aria-live="polite">
          <div class="hh-data-table__skeleton-list" aria-hidden="true">
            @for (item of [1, 2, 3, 4]; track item) {
              <div class="hh-data-table__skeleton-item"><span></span><span></span><span></span></div>
            }
          </div>
          <p>{{ loadingMessage() }}</p>
        </div>
      } @else if (error() && !rows().length) {
        <div class="hh-data-table__state hh-data-table__state--error" role="alert">
          <span class="material-icons" aria-hidden="true">error_outline</span>
          <p>{{ error() }}</p>
          <button type="button" class="hh-button hh-button--secondary" (click)="retry.emit()">{{ retryLabel() }}</button>
        </div>
      } @else if (configuredColumns().length > 0) {
        @if (error() && rows().length) { <div class="hh-data-table__stale-error" role="alert"><span class="material-icons" aria-hidden="true">error_outline</span><span>{{ error() }}</span><button type="button" class="hh-button hh-button--secondary hh-button--small" (click)="retry.emit()">Retry</button></div> }
        @if (loading()) { <div class="hh-data-table__loading-overlay" role="status" aria-live="polite"><span class="hh-spinner" aria-hidden="true"></span>{{ loadingMessage() }}</div> }
        <div class="hh-data-table__toolbar">
          @if (searchable()) {
            <div class="hh-data-table__search" role="search">
              <span class="material-icons" aria-hidden="true">search</span>
              <input type="search" [value]="searchValue()" [placeholder]="searchPlaceholder()" [attr.aria-label]="searchPlaceholder()" (input)="onSearchInput($event)" />
              @if (searchValue()) { <button type="button" class="hh-icon-button" aria-label="Clear search" title="Clear search" (click)="clearSearch()"><span class="material-icons" aria-hidden="true">close</span></button> }
            </div>
          }
          <span class="hh-data-table__selection" aria-live="polite">{{ selectionCount() }} selected</span>
          @if (bulkJob(); as job) {
            <span class="hh-data-table__bulk-job" role="status">{{ job.status }} {{ job.processed }}/{{ job.total }}</span>
            @if (job.rowProgress?.length) { <span class="hh-data-table__bulk-row-progress">{{ completedRows(job) }} rows completed</span> }
            @if (job.status === 'queued' || job.status === 'running') {
              <button type="button" class="hh-button hh-button--secondary hh-button--small" (click)="bulkJobCancelled.emit(job.jobId)">Cancel job</button>
            }
            @if (job.status === 'failed' && job.retryable) { <button type="button" class="hh-button hh-button--secondary hh-button--small" (click)="bulkJobRetry.emit(job.jobId)">Retry job</button> }
          }
          @if (exportable()) {
            <label class="hh-data-table__export">Export as
              <select [value]="exportFormat()" (change)="changeExportFormat($event)" aria-label="Export format">
                @for (format of exportFormats(); track format) { <option [value]="format">{{ format.toUpperCase() }}</option> }
              </select>
            </label>
            <button type="button" class="hh-button hh-button--secondary hh-button--small" (click)="requestExport()">Export</button>
            @if (exportJob(); as job) {
              <span class="hh-data-table__bulk-job" role="status">Export {{ job.status }} {{ job.processed }}/{{ job.total }}</span>
              @if (job.status === 'queued' || job.status === 'running') { <button type="button" class="hh-button hh-button--secondary hh-button--small" (click)="exportJobCancelled.emit(job.jobId)">Cancel export</button> }
              @if (job.status === 'failed' && job.retryable) { <button type="button" class="hh-button hh-button--secondary hh-button--small" (click)="exportJobRetry.emit(job.jobId)">Retry export</button> }
            }
          }
          @if (viewStorageKey()) {
            <button type="button" class="hh-icon-button" aria-label="Save table view" title="Save table view" (click)="saveView()"><span class="material-icons" aria-hidden="true">bookmark_add</span></button>
            <button type="button" class="hh-icon-button" aria-label="Reset table view" title="Reset table view" (click)="resetView()"><span class="material-icons" aria-hidden="true">restart_alt</span></button>
          }
          @if (filterBuilder()) {
            <details class="hh-data-table__filters">
              <summary aria-label="Open filters" title="Filters">
                <span class="material-icons hh-data-table__toolbar-icon" aria-hidden="true">filter_alt</span>
                <span class="hh-visually-hidden">Filters</span>
                <span class="hh-data-table__filter-count">{{ filterCount() }}</span>
              </summary>
              <div class="hh-data-table__filters-menu" role="group" aria-label="Advanced filters">
                @for (group of localFilterGroups(); track $index; let groupIndex = $index) {
                  <div class="hh-data-table__filter-group">
                    <strong>{{ group.join === 'and' ? 'All' : 'Any' }}</strong>
                    @for (filter of group.filters; track $index; let filterIndex = $index) {
                      <div class="hh-data-table__filter-row">
                        <select [value]="filter.key" aria-label="Filter field" (change)="updateFilter(groupIndex, filterIndex, 'key', $event)">
                          @for (column of configuredColumns(); track column.key) { <option [value]="column.key">{{ column.label }}</option> }
                        </select>
                        <select [value]="filter.operator" aria-label="Filter operator" (change)="updateFilter(groupIndex, filterIndex, 'operator', $event)">
                          @for (operator of filterOperators; track operator) { <option [value]="operator">{{ operatorLabel(operator) }}</option> }
                        </select>
                        <input [value]="filter.value ?? ''" aria-label="Filter value" (input)="updateFilter(groupIndex, filterIndex, 'value', $event)" />
                        <button type="button" class="hh-icon-button" aria-label="Remove filter" title="Remove filter" (click)="removeFilter(groupIndex, filterIndex)"><span class="material-icons" aria-hidden="true">close</span></button>
                      </div>
                    }
                    <button type="button" class="hh-button hh-button--secondary hh-button--small" (click)="addFilter(groupIndex)">Add condition</button>
                  </div>
                }
                <button type="button" class="hh-button hh-button--secondary hh-button--small" (click)="addFilterGroup()">Add group</button>
                <button type="button" class="hh-button hh-button--primary hh-button--small" (click)="applyFilters()">Apply filters</button>
              </div>
            </details>
          }
          @if (printable() || clipboard() || importable() || analysisOperations().length) {
            <div class="hh-data-table__utility-actions" role="group" aria-label="Table utilities">
              @if (clipboard()) { <button type="button" class="hh-icon-button" aria-label="Copy table" title="Copy table" (click)="copyTable()"><span class="material-icons" aria-hidden="true">content_copy</span></button> }
              @if (printable()) { <button type="button" class="hh-icon-button" aria-label="Print table" title="Print table" (click)="printTable()"><span class="material-icons" aria-hidden="true">print</span></button> }
              @if (importable()) { <button type="button" class="hh-icon-button" aria-label="Import data" title="Import data" (click)="importInput.click()"><span class="material-icons" aria-hidden="true">upload_file</span></button><input #importInput type="file" hidden accept=".csv,.xlsx" (change)="onImportFile($event)" /> }
              @for (operation of analysisOperations(); track operation) { <button type="button" class="hh-button hh-button--secondary hh-button--small" (click)="requestAnalysis(operation)">{{ operation }}</button> }
            </div>
          }
          @if (hasSelection() && bulkActions().length) {
            <div class="hh-data-table__bulk-actions" role="group" aria-label="Bulk actions">
              @for (action of bulkActions(); track action.id) {
                <button type="button" [class.hh-button]="!action.icon" [class.hh-button--secondary]="!action.icon && action.tone !== 'danger'" [class.hh-button--danger]="action.tone === 'danger' && !action.icon" [class.hh-button--small]="!action.icon" [class.hh-icon-button]="!!action.icon" [class.hh-icon-button--danger]="!!action.icon && action.tone === 'danger'" [disabled]="action.disabled || !canRunAction(action)" [hidden]="!canRunAction(action)" [attr.aria-label]="action.label" [attr.title]="action.icon ? action.label : null" (click)="runBulkAction(action)">
                  @if (action.icon) { <span class="material-icons" aria-hidden="true">{{ action.icon }}</span> } @else { {{ action.label }} }
                </button>
              }
            </div>
          }
          <details class="hh-data-table__columns">
            <summary>Columns</summary>
            <div class="hh-data-table__columns-menu">
              @for (column of configuredColumns(); track column.key) {
                @if (column.hideable !== false) {
                  <label><input type="checkbox" [checked]="isColumnVisible(column.key)" (change)="toggleColumn(column.key)" /> {{ column.label }}</label>
                }
              }
            </div>
          </details>
        </div>
        @if (!rows().length) {
          <div class="hh-data-table__state" role="status"><span class="material-icons" aria-hidden="true">{{ emptyIcon() }}</span><p>{{ emptyMessage() }}</p></div>
        } @else {
          <div class="hh-data-table__content" [class.hh-data-table__content--virtualized]="virtualize()" (scroll)="onVirtualScroll($event)">
            <table>
              <caption class="hh-visually-hidden">{{ label() }}</caption>
              <thead><tr>
                @if (selection()) { <th scope="col" class="hh-data-table__select"><input type="checkbox" [checked]="allVisibleSelected()" (change)="toggleAll()" aria-label="Select all rows" /></th> }
                @if (virtualizeColumns() && columnVirtualLeadingWidth() > 0) { <th class="hh-data-table__column-spacer" [style.width.px]="columnVirtualLeadingWidth()" aria-hidden="true"></th> }
                @for (column of renderedColumns(); track column.key) {
                  <th scope="col" [style.text-align]="column.align ?? 'start'" [style.width.px]="columnWidth(column)" [class.hh-data-table__cell--pin-left]="column.pinned === 'left'" [class.hh-data-table__cell--pin-right]="column.pinned === 'right'" [style.left.px]="pinnedOffset(column, 'left')" [style.right.px]="pinnedOffset(column, 'right')" [attr.draggable]="column.reorderable !== false" (dragstart)="beginColumnDrag(column.key)" (dragover)="$event.preventDefault()" (drop)="dropColumn(column.key)">
                    @if (column.sortable) {
                        <button type="button" class="hh-data-table__sort" (click)="sortBy(column.key, $event)" [attr.aria-label]="sortLabel(column)" [attr.aria-pressed]="sortStates().some(item => item.key === column.key)">
                        {{ column.label }} <span class="material-icons hh-data-table__sort-icon" aria-hidden="true">{{ sortIcon(column.key) }}</span>@if (sortPriority(column.key); as priority) { <span class="hh-data-table__sort-priority">{{ priority }}</span> }
                      </button>
                    } @else { {{ column.label }} }
                    @if (column.reorderable !== false) {
                      <button type="button" class="hh-data-table__reorder" [attr.aria-label]="'Reorder ' + column.label + ' column'" title="Reorder column" (click)="moveColumn(column.key, $event.shiftKey ? -1 : 1)" (keydown.arrowleft)="moveColumn(column.key, -1); $event.preventDefault()" (keydown.arrowright)="moveColumn(column.key, 1); $event.preventDefault()"><span class="material-icons" aria-hidden="true">drag_indicator</span></button>
                      <button type="button" class="hh-data-table__resize" role="separator" aria-orientation="vertical" [attr.aria-label]="'Resize ' + column.label + ' column'" [attr.aria-valuenow]="columnWidth(column) ?? 160" [attr.aria-valuemin]="column.minWidth ?? 96" [attr.aria-valuemax]="column.maxWidth ?? 640" title="Resize column" (pointerdown)="beginColumnResize($event, column)" (keydown.arrowleft)="resizeByKeyboard($any($event), column, -8)" (keydown.arrowright)="resizeByKeyboard($any($event), column, 8)"></button>
                    }
                  </th>
                }
                @if (virtualizeColumns() && columnVirtualTrailingWidth() > 0) { <th class="hh-data-table__column-spacer" [style.width.px]="columnVirtualTrailingWidth()" aria-hidden="true"></th> }
                @if (inlineEdit()) { <th scope="col" class="hh-data-table__edit-actions">Edit</th> }
              </tr></thead>
              <tbody>
                @if (virtualize() && virtualStart() > 0) { <tr aria-hidden="true"><td [attr.colspan]="visibleColumns().length + (selection() ? 1 : 0) + (inlineEdit() ? 1 : 0)" [style.height.px]="virtualStart() * virtualRowHeight()"></td></tr> }
                @for (row of renderedRows(); track rowKeyValue(row); let rowIndex = $index) {
                  <tr [class.hh-data-table__row--clickable]="rowClickable()" [attr.tabindex]="rowClickable() ? 0 : null" [attr.role]="rowClickable() ? 'button' : null" (click)="onRowClick(row)" (keydown.enter)="onRowClick(row)" (keydown.space)="$event.preventDefault(); onRowClick(row)" [attr.aria-label]="rowClickable() ? 'Open row ' + rowKeyValue(row) : null">
                    @if (selection()) { <td class="hh-data-table__select"><input type="checkbox" [checked]="isSelected(row)" (change)="toggleRow(row)" [attr.aria-label]="'Select row ' + rowKeyValue(row)" /></td> }
                    @if (virtualizeColumns() && columnVirtualLeadingWidth() > 0) { <td class="hh-data-table__column-spacer" [style.width.px]="columnVirtualLeadingWidth()" aria-hidden="true"></td> }
                    @for (column of renderedColumns(); track column.key; let columnIndex = $index) {
                      <td [style.text-align]="column.align ?? 'start'" [class.hh-data-table__cell--pin-left]="column.pinned === 'left'" [class.hh-data-table__cell--pin-right]="column.pinned === 'right'" [style.left.px]="pinnedOffset(column, 'left')" [style.right.px]="pinnedOffset(column, 'right')" [attr.tabindex]="keyboardNavigation() ? 0 : null" (keydown)="onCellKeydown($event, rowIndex, columnIndex)">
                        @if (columnIndex === 0 && (detailTemplate() || treeChildrenKey())) { <button type="button" class="hh-icon-button hh-icon-button--small" [attr.aria-label]="isExpanded(row) ? 'Collapse row' : 'Expand row'" [attr.title]="isExpanded(row) ? 'Collapse row' : 'Expand row'" (click)="toggleExpanded(row); $event.stopPropagation()"><span class="material-icons" aria-hidden="true">{{ isExpanded(row) ? 'expand_less' : 'expand_more' }}</span></button> }
                        @if (isEditing(row) && column.editable) {
                          <hh-table-editor [type]="column.editor ?? 'text'" [options]="column.options ?? []" [value]="draftValue(column.key)" [ariaLabel]="'Edit ' + column.label" [invalid]="!!editError(column.key)" [listId]="'hh-options-' + column.key" (valueChange)="updateDraftValue(column.key, $event)" />
                          @if (editError(column.key); as message) { <small class="hh-data-table__edit-error" role="alert">{{ message }}</small> }
                        } @else {
                          @if (cellTemplate(column.key); as template) {
                            <ng-container *ngTemplateOutlet="template.template; context: { $implicit: row, row: row, value: cellValue(row, column.key) }" />
                          } @else {
                            {{ cellValue(row, column.key) }}
                          }
                        }
                      </td>
                    }
                    @if (virtualizeColumns() && columnVirtualTrailingWidth() > 0) { <td class="hh-data-table__column-spacer" [style.width.px]="columnVirtualTrailingWidth()" aria-hidden="true"></td> }
                    @if (inlineEdit()) {
                      <td class="hh-data-table__edit-actions">
                        <div class="hh-data-table__edit-actions-content">
                        @if (isEditing(row)) {
                          @if (isSaving(row)) { <span class="hh-data-table__saving" role="status">Saving...</span> }
                          @if (editErrorMessage(); as message) { <small class="hh-data-table__save-error" role="alert">{{ message }}</small> }
                          @if (editErrorMessage()) { <button type="button" class="hh-button hh-button--secondary hh-button--small" (click)="retryEditSave(); $event.stopPropagation()">Retry</button> }
                          @if (editConflict(); as conflict) {
                            <small class="hh-data-table__save-error" role="alert">{{ conflict.message }}</small>
                            <button type="button" class="hh-button hh-button--secondary hh-button--small" (click)="resolveConflict('latest'); $event.stopPropagation()">Use latest</button>
                            <button type="button" class="hh-button hh-button--secondary hh-button--small" (click)="resolveConflict('mine'); $event.stopPropagation()">Keep mine</button>
                            @if (conflict.mergeableFields?.length) { <button type="button" class="hh-button hh-button--secondary hh-button--small" (click)="resolveConflict('merge'); $event.stopPropagation()">Merge</button> }
                          }
                          <span class="hh-data-table__dirty" [class.hh-data-table__dirty--visible]="isDirty(row)">{{ isDirty(row) ? 'Unsaved changes' : '' }}</span>
                          <button type="button" class="hh-button hh-button--primary hh-button--small" [disabled]="hasEditErrors() || isSaving(row)" (click)="saveEdit(row); $event.stopPropagation()">Save</button>
                          <button type="button" class="hh-button hh-button--secondary hh-button--small" (click)="cancelEdit(); $event.stopPropagation()">Cancel</button>
                          @if (lastEdit(); as transaction) { <button type="button" class="hh-button hh-button--secondary hh-button--small" (click)="undoEdit(transaction); $event.stopPropagation()">Undo</button> }
                        } @else {
                          <button type="button" class="hh-button hh-button--secondary hh-button--small" (click)="startEdit(row); $event.stopPropagation()">Edit</button>
                        }
                        </div>
                      </td>
                    }
                  </tr>
                  @if (isExpanded(row) && detailTemplate()) {
                    <tr class="hh-data-table__detail-row"><td [attr.colspan]="visibleColumns().length + (selection() ? 1 : 0) + (inlineEdit() ? 1 : 0)"><ng-container *ngTemplateOutlet="detailTemplate()!.template; context: { $implicit: row, row: row }" /></td></tr>
                  }
                }
                @if (virtualize() && virtualEnd() < pagedRows().length) { <tr aria-hidden="true"><td [attr.colspan]="visibleColumns().length + (selection() ? 1 : 0) + (inlineEdit() ? 1 : 0)" [style.height.px]="(pagedRows().length - virtualEnd()) * virtualRowHeight()"></td></tr> }
              </tbody>
            </table>
          </div>
          <div class="hh-data-table__mobile-list" aria-label="Mobile table list">
            @for (row of renderedRows(); track rowKeyValue(row)) {
              <article class="hh-data-table__mobile-item" [class.hh-data-table__row--clickable]="rowClickable() || !!detailTemplate()"
                       [attr.tabindex]="rowClickable() || !!detailTemplate() ? 0 : null" [attr.role]="rowClickable() || !!detailTemplate() ? 'button' : null"
                       [attr.aria-expanded]="detailTemplate() ? isExpanded(row) : null"
                       (click)="onMobileRowClick(row)" (keydown.enter)="onMobileRowClick(row)"
                       (keydown.space)="$event.preventDefault(); onMobileRowClick(row)">
                <header class="hh-data-table__mobile-item-header">
                  @if (selection()) { <input type="checkbox" [checked]="isSelected(row)" (click)="$event.stopPropagation()" (change)="toggleRow(row)" [attr.aria-label]="'Select row ' + rowKeyValue(row)" /> }
                  <strong>{{ mobileItemTitle(row) }}</strong>
                  @if (inlineEdit()) {
                    @if (isEditing(row)) {
                      <button type="button" class="hh-button hh-button--secondary hh-button--small" (click)="cancelEdit(); $event.stopPropagation()">Cancel</button>
                    } @else {
                      <button type="button" class="hh-button hh-button--secondary hh-button--small" (click)="startEdit(row); $event.stopPropagation()">Edit</button>
                    }
                  }
                </header>
                <dl class="hh-data-table__mobile-fields">
                  @for (column of visibleColumns(); track column.key) {
                    @if (column.key !== mobileTitleKey()) {
                      <div>
                        <dt>{{ column.label }}</dt>
                        <dd>
                          @if (isEditing(row) && column.editable) {
                            <hh-table-editor [type]="column.editor ?? 'text'" [options]="column.options ?? []" [value]="draftValue(column.key)" [ariaLabel]="'Edit ' + column.label" [invalid]="!!editError(column.key)" [listId]="'hh-mobile-options-' + column.key" (valueChange)="updateDraftValue(column.key, $event)" />
                            @if (editError(column.key); as message) { <small class="hh-data-table__edit-error" role="alert">{{ message }}</small> }
                          } @else if (column.status) {
                            <hh-status-badge [status]="displayValue(row, column.key)" [label]="displayValue(row, column.key)" />
                          } @else {
                            {{ cellValue(row, column.key) }}
                          }
                        </dd>
                      </div>
                    }
                  }
                </dl>
                @if (detailTemplate() && isExpanded(row)) {
                  <div class="hh-data-table__mobile-detail" role="region"><ng-container *ngTemplateOutlet="detailTemplate()!.template; context: { $implicit: row, row: row }" /></div>
                }
                @if (isEditing(row)) {
                  <footer class="hh-data-table__mobile-item-actions">
                    @if (isSaving(row)) { <span class="hh-data-table__saving" role="status">Saving...</span> }
                    <button type="button" class="hh-button hh-button--primary hh-button--small" [disabled]="hasEditErrors() || isSaving(row)" (click)="saveEdit(row); $event.stopPropagation()">Save</button>
                  </footer>
                }
              </article>
            }
          </div>
           <nav class="hh-data-table__pagination" aria-label="Table pagination">
             @if (cursorPaging()) {
               <button type="button" class="hh-button hh-button--secondary" [disabled]="!previousCursor()" (click)="changeCursor(previousCursor())" aria-label="Previous page">Previous</button>
               <span>Cursor page</span>
               <button type="button" class="hh-button hh-button--secondary" [disabled]="!nextCursor()" (click)="changeCursor(nextCursor())" aria-label="Next page">Next</button>
             } @else {
             <label class="hh-data-table__page-size">Rows <select [value]="pageSizeValue()" (change)="changePageSize($event)"><option [value]="10">10</option><option [value]="20">20</option><option [value]="50">50</option></select></label>
             <span>{{ pageStart() }}-{{ pageEnd() }} of {{ totalRows() }}</span>
             <button type="button" class="hh-button hh-button--secondary" [disabled]="pageValue() <= 1" (click)="changePage(pageValue() - 1)" aria-label="Previous page">Previous</button>
             <span aria-current="page">Page {{ pageValue() }} of {{ pageCount() }}</span>
             <button type="button" class="hh-button hh-button--secondary" [disabled]="pageValue() >= pageCount()" (click)="changePage(pageValue() + 1)" aria-label="Next page">Next</button>
             }
           </nav>
        }
      } @else if (empty()) {
        <div class="hh-data-table__state" role="status">
          <span class="material-icons" aria-hidden="true">{{ emptyIcon() }}</span>
          <p>{{ emptyMessage() }}</p>
        </div>
      } @else {
        <div class="hh-data-table__content"><ng-content /></div>
      }
    </section>
  `,
  styles: [`
    :host { display: block; min-width: 0; }
    .hh-data-table { position: relative; overflow: visible; border: 1px solid var(--border-default); border-radius: var(--radius-card); background: var(--surface-white); }
    .hh-data-table__loading-overlay { position: absolute; inset: 0; z-index: 3; display: flex; justify-content: center; gap: 8px; padding-top: 72px; background: color-mix(in srgb, var(--surface-white) 72%, transparent); color: var(--text-secondary); pointer-events: none; }
    .hh-data-table__stale-error { position: relative; z-index: 4; display: flex; align-items: center; gap: 8px; padding: 8px 12px; border-bottom: 1px solid color-mix(in srgb, var(--color-danger) 25%, var(--border-light)); background: color-mix(in srgb, var(--color-danger) 8%, var(--surface-white)); color: var(--color-danger); font-size: var(--font-size-caption); }
    .hh-data-table__stale-error span:nth-child(2) { flex: 1; }
    .hh-data-table__content { position: relative; z-index: 1; min-width: 0; overflow-x: auto; }
    .hh-data-table__content--virtualized { max-height: 560px; overflow: auto; }
    .hh-data-table__mobile-list { display: none; }
    .hh-data-table__mobile-item { display: grid; gap: 10px; min-height: 64px; box-sizing: border-box; padding: 12px 16px; border-bottom: 1px solid var(--border-light); background: var(--surface-white); }
    .hh-data-table__mobile-item:last-child { border-bottom: 0; }
    .hh-data-table__mobile-item-header { display: flex; align-items: center; gap: 10px; min-width: 0; }
    .hh-data-table__mobile-item-header strong { flex: 1; min-width: 0; overflow: hidden; color: var(--text-primary); text-overflow: ellipsis; white-space: nowrap; }
    .hh-data-table__mobile-fields { display: grid; gap: 10px; margin: 0; }
    .hh-data-table__mobile-fields > div { display: grid; grid-template-columns: minmax(96px, 38%) minmax(0, 1fr); gap: 12px; align-items: start; }
    .hh-data-table__mobile-fields dt { color: var(--text-secondary); font-size: var(--font-size-caption); font-weight: var(--font-weight-semibold); }
    .hh-data-table__mobile-fields dd { min-width: 0; margin: 0; overflow-wrap: anywhere; color: var(--text-primary); display: -webkit-box; overflow: hidden; -webkit-box-orient: vertical; -webkit-line-clamp: 2; }
    .hh-data-table__mobile-fields dd hh-status-badge { display: inline-flex; }
    .hh-data-table__mobile-detail { padding: 12px; border-radius: var(--radius-input); background: var(--surface-subtle); color: var(--text-secondary); line-height: 1.5; }
    .hh-data-table__mobile-item-actions { display: flex; justify-content: flex-end; gap: 8px; }
    .hh-data-table__content table { width: 100%; min-width: 640px; table-layout: fixed; border-collapse: collapse; }
    .hh-data-table th, .hh-data-table td { box-sizing: border-box; padding: 12px 16px; border-bottom: 1px solid var(--border-light); color: var(--text-primary); overflow-wrap: anywhere; }
    .hh-data-table th { position: relative; min-width: 96px; }
    .hh-data-table__row--clickable { cursor: pointer; }
    .hh-data-table__row--clickable:hover { background: var(--surface-hover); }
    .hh-data-table__row--clickable:focus-visible { outline: 2px solid var(--color-focus); outline-offset: -2px; }
    .hh-data-table th { color: var(--text-secondary); font-size: var(--font-size-label); font-weight: var(--font-weight-semibold); text-transform: uppercase; letter-spacing: .04em; }
    .hh-data-table--compact th, .hh-data-table--compact td { padding: 8px 12px; }
    .hh-data-table__toolbar, .hh-data-table__pagination { display: flex; align-items: center; justify-content: flex-end; flex-wrap: wrap; gap: 12px; padding: 10px 12px; border-bottom: 1px solid var(--border-light); }
    .hh-data-table__toolbar { position: relative; z-index: 10; overflow: visible; }
    .hh-data-table__search { display: flex; flex: 1 1 260px; align-items: center; gap: var(--space-2); min-width: min(260px, 100%); min-height: var(--control-height); padding: 0 var(--space-2) 0 var(--space-3); border: 1px solid var(--border-default); border-radius: var(--radius-input); background: var(--surface-white); color: var(--text-secondary); }
    .hh-data-table__search:focus-within { border-color: var(--color-focus); outline: 2px solid color-mix(in srgb, var(--color-focus) 25%, transparent); outline-offset: 1px; }
    .hh-data-table__search > .material-icons { font-size: 20px; }
    .hh-data-table__search input { min-width: 0; flex: 1; height: 100%; border: 0; outline: 0; background: transparent; color: var(--text-primary); font: inherit; }
    .hh-data-table__search input[type="search"]::-webkit-search-cancel-button,
    .hh-data-table__search input[type="search"]::-webkit-search-decoration { -webkit-appearance: none; appearance: none; }
    .hh-data-table__search input[type="search"]::-ms-clear { display: none; width: 0; height: 0; }
    .hh-data-table__search input::placeholder { color: var(--text-muted); }
    .hh-data-table__search .hh-icon-button { width: 32px; height: 32px; min-width: 32px; }
     .hh-data-table__selection { margin-right: auto; color: var(--text-secondary); font-size: var(--font-size-caption); }
     .hh-data-table__bulk-job { color: var(--text-secondary); font-size: var(--font-size-caption); }
    .hh-data-table__sort { display: inline-flex; align-items: center; gap: 3px; min-height: 32px; padding: 2px 4px; border: 0; border-radius: 4px; background: transparent; color: inherit; font: inherit; cursor: pointer; }
    .hh-data-table__sort:hover, .hh-data-table__sort:focus-visible { background: var(--surface-hover); }
    .hh-data-table__sort-icon { display: inline-grid; place-items: center; width: 16px; height: 16px; color: var(--text-muted); font-size: 16px; font-weight: 400; line-height: 1; }
    .hh-data-table__sort:hover .hh-data-table__sort-icon, .hh-data-table__sort:focus-visible .hh-data-table__sort-icon { color: var(--color-primary); }
    .hh-data-table__sort-priority { display: inline-grid; place-items: center; min-width: 14px; height: 14px; border-radius: 50%; background: var(--color-primary); color: var(--surface-white); font-size: 9px; line-height: 1; }
    .hh-data-table__resize { position: absolute; top: 50%; right: 0; width: 6px; height: 22px; padding: 0; border: 0; border-right: 1px solid transparent; background: transparent; transform: translateY(-50%); cursor: col-resize; }
    .hh-data-table__reorder { position: absolute; top: 50%; right: 10px; z-index: 1; display: inline-grid; place-items: center; width: 20px; height: 20px; padding: 0; border: 0; border-radius: 4px; background: transparent; color: var(--text-muted); opacity: .42; transform: translateY(-50%); cursor: grab; }
    .hh-data-table__reorder .material-icons { font-size: 16px; }
    .hh-data-table th:hover .hh-data-table__reorder, .hh-data-table__reorder:focus-visible { opacity: 1; background: var(--surface-hover); }
    .hh-data-table__reorder:focus-visible { outline: 2px solid var(--color-focus); outline-offset: 1px; }
    .hh-data-table__resize:hover, .hh-data-table__resize:focus-visible { border-right-color: var(--color-focus); outline: 0; }
    .hh-data-table__select { width: 56px; min-width: 56px; padding-inline: 8px !important; }
    .hh-data-table__edit-actions { width: 148px; min-width: 148px; padding: var(--space-2) var(--space-3) !important; text-align: start !important; white-space: normal; vertical-align: middle; }
    .hh-data-table__edit-actions-content { display: flex; flex-wrap: wrap; align-items: center; justify-content: flex-start; gap: var(--space-2); min-width: 128px; }
    .hh-data-table__edit-input { width: 100%; min-height: 40px; box-sizing: border-box; border: 1px solid var(--border-default); border-radius: var(--radius-input); padding: 0 10px; background: var(--surface-white); color: var(--text-primary); font: inherit; }
    .hh-data-table__edit-input[aria-invalid="true"] { border-color: var(--color-danger); }
    .hh-data-table__edit-error { display: block; margin-top: 4px; color: var(--color-danger); font-size: var(--font-size-caption); }
    .hh-data-table__saving { color: var(--text-secondary); font-size: var(--font-size-caption); }
    .hh-data-table__save-error { color: var(--color-danger); font-size: var(--font-size-caption); }
    .hh-data-table__dirty { display: none; color: var(--color-warning); font-size: var(--font-size-caption); }
    .hh-data-table__dirty--visible { display: inline; }
    .hh-data-table__edit-input:focus { outline: 2px solid var(--color-focus); outline-offset: 1px; }
    .hh-data-table .hh-button--small { min-height: 32px; padding: 0 10px; font-size: var(--font-size-caption); }
    .hh-data-table__columns { position: relative; z-index: 20; }
    .hh-data-table__columns summary { cursor: pointer; color: var(--text-secondary); font-size: var(--font-size-caption); }
    .hh-data-table__columns-menu { position: absolute; top: calc(100% + 8px); right: 0; z-index: 30; display: grid; gap: 8px; min-width: 180px; padding: 12px; border: 1px solid var(--border-default); border-radius: var(--radius-card); background: var(--surface-white); box-shadow: var(--shadow-dropdown); }
    .hh-data-table__columns-menu label { display: flex; gap: 8px; white-space: normal; overflow-wrap: anywhere; font-size: var(--font-size-caption); }
    .hh-data-table__filters { position: relative; z-index: 20; }
    .hh-data-table__filters > summary { display: inline-flex; align-items: center; justify-content: center; gap: 4px; min-width: 34px; height: 34px; padding: 0 7px; border: 1px solid var(--border-default); border-radius: var(--radius-control); background: var(--surface-white); cursor: pointer; color: var(--text-secondary); font-size: var(--font-size-caption); list-style: none; }
    .hh-data-table__filters > summary::-webkit-details-marker { display: none; }
    .hh-data-table__toolbar-icon { font-size: 18px; line-height: 1; }
    .hh-data-table__filter-count { display: inline-grid; place-items: center; min-width: 18px; height: 18px; margin-left: 4px; border-radius: 50%; background: var(--color-primary); color: var(--surface-white); font-size: 10px; }
    .hh-data-table__filters-menu { position: absolute; top: calc(100% + 8px); right: 0; z-index: 30; display: grid; gap: 12px; min-width: min(620px, calc(100vw - 32px)); padding: 14px; border: 1px solid var(--border-default); border-radius: var(--radius-card); background: var(--surface-white); box-shadow: var(--shadow-dropdown); }
    .hh-data-table__utility-actions { display: inline-flex; align-items: center; gap: 6px; }
    .hh-data-table__filter-group { display: grid; gap: 8px; padding-bottom: 10px; border-bottom: 1px solid var(--border-light); }
    .hh-data-table__filter-row { display: grid; grid-template-columns: minmax(120px, 1fr) 120px minmax(120px, 1fr) 40px; gap: 8px; align-items: center; }
    .hh-data-table__filter-row select, .hh-data-table__filter-row input { min-height: 36px; min-width: 0; padding: 0 8px; border: 1px solid var(--border-default); border-radius: var(--radius-input); background: var(--surface-white); color: var(--text-primary); font: inherit; }
    .hh-data-table__cell--pin-left, .hh-data-table__cell--pin-right { position: sticky; z-index: 2; background: var(--surface-white); box-shadow: 1px 0 0 var(--border-light); }
    .hh-data-table__cell--pin-right { box-shadow: -1px 0 0 var(--border-light); }
    .hh-data-table--sticky-header thead th { position: sticky; top: 0; z-index: 3; background: var(--surface-white); }
    .hh-data-table--sticky-header thead th.hh-data-table__cell--pin-left, .hh-data-table--sticky-header thead th.hh-data-table__cell--pin-right { z-index: 4; }
    .hh-data-table--sticky-footer .hh-data-table__pagination { position: sticky; bottom: 0; z-index: 3; background: var(--surface-white); }
    .hh-data-table__detail-row td { padding: 16px; background: var(--surface-subtle); }
    .hh-data-table__export { display: inline-flex; align-items: center; gap: 6px; color: var(--text-secondary); font-size: var(--font-size-caption); }
    .hh-data-table__export select { min-height: 32px; border: 1px solid var(--border-default); border-radius: var(--radius-input); background: var(--surface-white); color: var(--text-primary); padding: 0 8px; }
    .hh-data-table__select { text-align: center; }
    .hh-data-table__pagination { justify-content: flex-end; border-top: 1px solid var(--border-light); border-bottom: 0; font-size: var(--font-size-caption); color: var(--text-secondary); }
    .hh-data-table__pagination .hh-button { min-height: 32px; }
    .hh-data-table__page-size { display: inline-flex; align-items: center; gap: 6px; color: var(--text-secondary); }
    .hh-data-table__page-size select { min-height: 32px; border: 1px solid var(--border-default); border-radius: var(--radius-input); background: var(--surface-white); color: var(--text-primary); padding: 0 8px; }
    .hh-visually-hidden { position: absolute; width: 1px; height: 1px; padding: 0; margin: -1px; overflow: hidden; clip: rect(0, 0, 0, 0); white-space: nowrap; border: 0; }
    .hh-data-table__state { display: grid; place-items: center; gap: 12px; min-height: 180px; padding: 32px 24px; color: var(--text-muted); text-align: center; }
    .hh-data-table__state p { margin: 0; }
    .hh-data-table__state .material-icons { font-size: 40px; width: 40px; height: 40px; opacity: .65; }
    .hh-data-table__state--error { color: var(--color-danger); }
    .hh-data-table__skeleton-list { display: grid; width: min(100%, 420px); gap: 10px; }
    .hh-data-table__skeleton-item { display: grid; gap: 8px; padding: 14px; border: 1px solid var(--border-light); border-radius: var(--radius-input); background: var(--surface-white); }
    .hh-data-table__skeleton-item span { display: block; height: 10px; border-radius: 6px; background: linear-gradient(90deg, var(--surface-subtle), var(--border-light), var(--surface-subtle)); background-size: 200% 100%; animation: hh-skeleton-shimmer 1.3s linear infinite; }
    .hh-data-table__skeleton-item span:first-child { width: 62%; height: 14px; }
    .hh-data-table__skeleton-item span:last-child { width: 42%; }
    @keyframes hh-skeleton-shimmer { to { background-position: -200% 0; } }
    .hh-data-table--compact .hh-data-table__state { min-height: 140px; padding: 24px 16px; }
    @media (max-width: 768px) {
      .hh-data-table { border-radius: var(--radius-input); }
      .hh-data-table__toolbar, .hh-data-table__pagination { justify-content: flex-start; gap: 8px; padding: 8px; }
      .hh-data-table__selection { flex: 1 0 100%; margin-right: 0; }
      .hh-data-table__bulk-actions { display: flex; flex: 1 0 100%; flex-wrap: wrap; gap: 8px; }
      .hh-data-table__pagination > * { max-width: 100%; }
      .hh-data-table__pagination .hh-button { flex: 1 1 auto; }
      .hh-data-table__columns-menu { right: auto; left: 0; width: min(240px, calc(100vw - 32px)); min-width: 0; }
      .hh-data-table__filters-menu { right: auto; left: 0; width: min(620px, calc(100vw - 32px)); min-width: 0; }
      .hh-data-table__filter-row { grid-template-columns: 1fr; }
    }
    @media (max-width: 420px) {
      .hh-data-table__export { flex: 1 0 100%; }
      .hh-data-table__export select { flex: 1; min-width: 0; }
      .hh-data-table__pagination { font-size: 11px; }
    }
    @media (max-width: 600px) {
      .hh-data-table--mobile-list .hh-data-table__content { display: none; }
      .hh-data-table--mobile-list .hh-data-table__mobile-list { display: grid; }
      .hh-data-table__mobile-fields > div { grid-template-columns: 1fr; gap: 3px; }
    }
    @media print {
      .hh-data-table__toolbar, .hh-data-table__pagination, .hh-data-table__mobile-list, .hh-data-table__edit-actions, .hh-data-table__reorder, .hh-data-table__resize { display: none !important; }
      .hh-data-table__content { overflow: visible !important; max-height: none !important; }
      .hh-data-table { border: 0; box-shadow: none; }
    }
  `],
})
export class HisHopeDataTableComponent {
  readonly label = input('Data table');
  readonly loading = input(false);
  readonly loadingMessage = input('Loading data...');
  readonly empty = input(false);
  readonly emptyMessage = input('No data available.');
  readonly emptyIcon = input('inbox');
  readonly error = input('');
  readonly retryLabel = input('Retry');
  readonly density = input<HisHopeDataTableDensity>('comfortable');
  readonly mobilePresentation = input<HisHopeDataTableMobilePresentation>('auto');
  readonly columns = input<HisHopeDataTableColumn[]>([]);
  readonly rows = input<Record<string, unknown>[]>([]);
  readonly pageSize = input(10);
  readonly page = input(1);
  readonly selection = input(false);
  readonly selectionScope = input<HisHopeTableSelectionScope>('page');
  readonly selectionState = input<HisHopeTableSelectionState | null>(null);
  readonly sort = input<HisHopeSort | null>(null);
  readonly hiddenColumns = input<string[]>([]);
  readonly rowClickable = input(false);
  readonly inlineEdit = input(false);
  readonly mode = input<HisHopeDataTableMode>('client');
  readonly totalItems = input<number | null>(null);
  readonly nextCursor = input<string | null>(null);
  readonly previousCursor = input<string | null>(null);
  readonly exportable = input(false);
  readonly exportFormats = input<HisHopeTableExportFormat[]>(['csv']);
  readonly bulkActions = input<HisHopeBulkAction[]>([]);
  readonly query = input<HisHopePageQuery>({ page: 1, pageSize: 10 });
  readonly virtualize = input(false);
  readonly virtualizeColumns = input(false);
  readonly virtualRowHeight = input(56);
  readonly stickyHeader = input(true);
  readonly stickyFooter = input(false);
  readonly keyboardNavigation = input(true);
  readonly searchable = input(true);
  readonly searchPlaceholder = input('Search table...');
  readonly filterBuilder = input(false);
  readonly filterGroups = input<HisHopeFilterGroup[]>([]);
  readonly viewStorageKey = input('');
  readonly viewName = input('default');
  readonly serverBackedView = input(false);
  readonly savedView = input<{ hiddenColumns?: string[]; columnOrder?: string[]; columnWidths?: Record<string, number>; sort?: HisHopeSort | null } | null>(null);
  readonly permissionChecker = input<(permission?: string) => boolean>(() => true);
  readonly maskSensitiveExport = input(true);
  readonly printable = input(false);
  readonly clipboard = input(false);
  readonly importable = input(false);
  readonly analysisOperations = input<Array<'pivot' | 'aggregate' | 'formula'>>([]);
  readonly groupBy = input<string | null>(null);
  readonly treeChildrenKey = input<string | null>(null);
  readonly expandedRowKeys = input<string[]>([]);
  readonly bulkJob = input<HisHopeBulkJob | null>(null);
  readonly exportJob = input<HisHopeBulkJob | null>(null);
  readonly urlSync = input(true);
  readonly savingRowKey = input<string | null>(null);
  readonly editState = input<HisHopeTableEditState | null>(null);
  readonly editConflict = input<HisHopeTableEditConflict | null>(null);
  readonly columnOrder = input<string[]>([]);
  @ContentChildren(HisHopeDataTableCellDirective) readonly cellTemplates!: QueryList<HisHopeDataTableCellDirective>;
  @ContentChildren(HisHopeDataTableDetailDirective) readonly detailTemplates!: QueryList<HisHopeDataTableDetailDirective>;

  @Output() readonly retry = new EventEmitter<void>();
  @Output() readonly pageChange = new EventEmitter<number>();
  @Output() readonly sortChange = new EventEmitter<HisHopeSort>();
  @Output() readonly selectionChange = new EventEmitter<Record<string, unknown>[]>();
  @Output() readonly selectionStateChange = new EventEmitter<HisHopeTableSelectionState>();
  @Output() readonly rowClick = new EventEmitter<Record<string, unknown>>();
  @Output() readonly rowEditSave = new EventEmitter<Record<string, unknown>>();
  @Output() readonly rowEditCancel = new EventEmitter<void>();
  @Output() readonly pageSizeChange = new EventEmitter<number>();
  @Output() readonly exportRequested = new EventEmitter<HisHopeTableExportRequest>();
  @Output() readonly bulkActionRequested = new EventEmitter<HisHopeBulkActionRequest>();
  @Output() readonly queryChange = new EventEmitter<HisHopePageQuery>();
  @Output() readonly columnOrderChange = new EventEmitter<string[]>();
  @Output() readonly columnResizeChange = new EventEmitter<{ key: string; width: number }>();
  @Output() readonly rowEditSaveRequested = new EventEmitter<HisHopeTableEditSaveRequest>();
  @Output() readonly rowEditUndo = new EventEmitter<{ previous: Record<string, unknown>; current: Record<string, unknown> }>();
  @Output() readonly rowEditConflict = new EventEmitter<HisHopeTableEditConflict>();
  @Output() readonly conflictResolved = new EventEmitter<{ resolution: HisHopeConflictResolution; conflict: HisHopeTableEditConflict }>();
  @Output() readonly bulkJobCancelled = new EventEmitter<string>();
  @Output() readonly exportJobCancelled = new EventEmitter<string>();
  @Output() readonly bulkJobRetry = new EventEmitter<string>();
  @Output() readonly exportJobRetry = new EventEmitter<string>();
  @Output() readonly rowExpandChange = new EventEmitter<{ rowKey: string; expanded: boolean }>();
  @Output() readonly importRequested = new EventEmitter<HisHopeTableImportRequest>();
  @Output() readonly analysisRequested = new EventEmitter<HisHopeTableAnalysisRequest>();
  @Output() readonly filterChange = new EventEmitter<HisHopeFilterGroup[]>();
  @Output() readonly viewChange = new EventEmitter<{ hiddenColumns: string[]; columnOrder: string[]; columnWidths: Record<string, number>; sort: HisHopeSort | null }>();
  @Output() readonly viewSaveRequested = new EventEmitter<{ name: string; payload: { hiddenColumns: string[]; columnOrder: string[]; columnWidths: Record<string, number>; sort: HisHopeSort | null } }>();
  @Output() readonly viewResetRequested = new EventEmitter<{ name: string }>();
  readonly selectedRows = new Set<string>();
  private readonly localHiddenColumns = signal<string[]>([]);
  private readonly responsiveVisibleColumns = signal<string[]>([]);
  private readonly viewportWidth = signal(1024);
  private readonly localSort = signal<HisHopeSort | null>(null);
  private readonly localPage = signal(1);
  private readonly localPageSize = signal(10);
  private readonly editingRowKey = signal<string | null>(null);
  private readonly draftRow = signal<Record<string, unknown>>({});
  private readonly draftErrors = signal<Record<string, string>>({});
  private readonly localColumnOrder = signal<string[]>([]);
  private readonly localColumnWidths = signal<Record<string, number>>({});
  readonly virtualStart = signal(0);
  readonly columnVirtualStart = signal(0);
  private readonly columnVirtualViewport = signal(1200);
  readonly lastEdit = signal<{ previous: Record<string, unknown>; current: Record<string, unknown> } | null>(null);
  private readonly localExportFormat = signal<HisHopeTableExportFormat>('csv');
  readonly localFilterGroups = signal<HisHopeFilterGroup[]>([]);
  readonly searchValue = signal('');
  readonly filterOperators: HisHopeFilterOperator[] = ['eq', 'neq', 'contains', 'startsWith', 'gt', 'gte', 'lt', 'lte', 'in'];
  private restoredViewKey: string | null = null;
  private restoredServerView: object | null = null;
  private draggingColumnKey: string | null = null;
  private resizingColumnKey: string | null = null;
  private resizeStartX = 0;
  private resizeStartWidth = 0;
  private previousEditRow: Record<string, unknown> | null = null;
  private selectAllPages = false;
  private readonly excludedRows = new Set<string>();
  private lastSaveRequest: HisHopeTableEditSaveRequest | null = null;
  private lastConflict: HisHopeTableEditConflict | null = null;
  private readonly router = inject(Router, { optional: true });
  private readonly route = inject(ActivatedRoute, { optional: true });
  private readonly destroyRef = inject(DestroyRef);
  private urlReady = false;
  private searchTimer: ReturnType<typeof setTimeout> | undefined;
  private searchPending = false;

  constructor() {
    if (typeof window !== 'undefined') this.viewportWidth.set(window.innerWidth);
    effect(() => {
      const key = this.viewStorageKey();
      if (key && key !== this.restoredViewKey) this.restoreSavedView(key);
    });
    effect(() => {
      const saved = this.savedView();
      if (!saved || saved === this.restoredServerView) return;
      this.restoredServerView = saved;
      this.applySavedView(saved);
    });
    effect(() => {
      const incoming = this.filterGroups();
      if (incoming.length && !this.localFilterGroups().length) this.localFilterGroups.set(incoming);
    });
    effect(() => {
      const incoming = this.query().search ?? '';
      if (!this.searchPending) this.searchValue.set(incoming);
    });
    effect(() => {
      const incoming = this.selectionState();
      if (!incoming) return;
      this.selectAllPages = incoming.scope === 'all' && incoming.selectedKeys.length === 0;
      this.selectedRows.clear();
      this.excludedRows.clear();
      incoming.selectedKeys.forEach(key => this.selectedRows.add(key));
      incoming.excludedKeys.forEach(key => this.excludedRows.add(key));
    });
    effect(() => {
      const conflict = this.editConflict();
      if (conflict && conflict !== this.lastConflict) {
        this.lastConflict = conflict;
        this.rowEditConflict.emit(conflict);
      }
    });
    this.route?.queryParams.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(params => {
      if (!this.urlSync()) return;
      const next: HisHopePageQuery = { ...this.query(), ...parseHisHopeDataTableQuery(params as Record<string, string | null | undefined>) };
      this.urlReady = true;
      this.queryChange.emit(next);
    });
    effect(() => {
      const query = this.query();
      if (!this.urlSync() || !this.urlReady || !this.router) return;
      void this.router.navigate([], { relativeTo: this.route ?? undefined, queryParams: serializeHisHopeDataTableQuery(query), queryParamsHandling: 'merge', replaceUrl: true });
    });
    this.destroyRef.onDestroy(() => { if (this.searchTimer) clearTimeout(this.searchTimer); });
  }

  @HostListener('window:resize') onViewportResize(): void { if (typeof window !== 'undefined') this.viewportWidth.set(window.innerWidth); }

  onSearchInput(event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    this.searchValue.set(value);
    this.searchPending = true;
    if (this.searchTimer) clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => {
      this.searchPending = false;
      const search = value.trim();
      this.queryChange.emit({ ...this.query(), page: 1, cursor: undefined, search: search || undefined });
    }, 350);
  }

  clearSearch(): void {
    if (this.searchTimer) clearTimeout(this.searchTimer);
    this.searchPending = false;
    this.searchValue.set('');
    this.queryChange.emit({ ...this.query(), page: 1, cursor: undefined, search: undefined });
  }

  configuredColumns(): HisHopeDataTableColumn[] { return this.columns(); }
  visibleColumns(): HisHopeDataTableColumn[] {
    const hidden = this.localHiddenColumns().length ? this.localHiddenColumns() : this.hiddenColumns();
    const responsiveVisible = this.responsiveVisibleColumns();
    const order = this.localColumnOrder().length ? this.localColumnOrder() : this.columnOrder();
    const columns = order.length ? [...this.columns()].sort((a, b) => this.orderIndex(order, a.key) - this.orderIndex(order, b.key)) : this.columns();
    return columns.filter(column => !hidden.includes(column.key) && (!this.isResponsiveHidden(column) || responsiveVisible.includes(column.key)));
  }
  renderedColumns(): HisHopeDataTableColumn[] {
    const columns = this.visibleColumns();
    if (!this.virtualizeColumns() || columns.length < 12) return columns;
    const left = columns.filter(column => column.pinned === 'left');
    const right = columns.filter(column => column.pinned === 'right');
    const middle = columns.filter(column => !column.pinned);
    const width = 160;
    const slots = Math.max(1, Math.ceil(this.columnVirtualViewport() / width) + 2);
    const start = Math.min(this.columnVirtualStart(), Math.max(0, middle.length - slots));
    return [...left, ...middle.slice(start, start + slots), ...right];
  }
  columnVirtualLeadingWidth(): number {
    if (!this.virtualizeColumns() || this.visibleColumns().length < 12) return 0;
    const middle = this.visibleColumns().filter(column => !column.pinned);
    const left = this.visibleColumns().filter(column => column.pinned === 'left').length;
    const start = Math.min(this.columnVirtualStart(), Math.max(0, middle.length - (Math.ceil(this.columnVirtualViewport() / 160) + 2)));
    return left ? 0 : start * 160;
  }
  columnVirtualTrailingWidth(): number {
    if (!this.virtualizeColumns() || this.visibleColumns().length < 12) return 0;
    const columns = this.visibleColumns();
    const middle = columns.filter(column => !column.pinned);
    const left = columns.filter(column => column.pinned === 'left');
    const right = columns.filter(column => column.pinned === 'right');
    const slots = Math.max(1, Math.ceil(this.columnVirtualViewport() / 160) + 2);
    const start = Math.min(this.columnVirtualStart(), Math.max(0, middle.length - slots));
    return Math.max(0, middle.length - start - slots) * 160;
  }
  mobileTitleKey(): string { return this.visibleColumns()[0]?.key ?? ''; }
  mobileItemTitle(row: Record<string, unknown>): string { return this.cellValue(row, this.mobileTitleKey()) || this.rowKeyValue(row); }
  private orderIndex(order: string[], key: string): number { const index = order.indexOf(key); return index < 0 ? order.length : index; }
  columnWidth(column: HisHopeDataTableColumn): number | null { const width = this.localColumnWidths()[column.key]; if (width) return width; const parsed = column.width ? Number.parseFloat(column.width) : NaN; return Number.isFinite(parsed) ? parsed : null; }
  pinnedOffset(column: HisHopeDataTableColumn, side: 'left' | 'right'): number | null {
    if (column.pinned !== side) return null;
    const columns = this.visibleColumns().filter(item => item.pinned === side);
    const index = columns.findIndex(item => item.key === column.key);
    if (index < 0) return null;
    return columns.slice(0, index).reduce((total, item) => total + (this.columnWidth(item) ?? 160), 0);
  }
  filterCount(): number { return this.localFilterGroups().reduce((count, group) => count + group.filters.filter(filter => filter.value !== '' && filter.value !== null).length, 0); }
  operatorLabel(operator: HisHopeFilterOperator): string {
    return ({ eq: '=', neq: '≠', contains: '∋', startsWith: 'A*', gt: '>', gte: '≥', lt: '<', lte: '≤', in: '∈' } as Record<HisHopeFilterOperator, string>)[operator];
  }
  addFilterGroup(): void { this.localFilterGroups.update(groups => [...groups, { join: 'and', filters: [{ key: this.configuredColumns()[0]?.key ?? '', operator: 'contains', value: '' }] }]); }
  addFilter(groupIndex: number): void { this.localFilterGroups.update(groups => groups.map((group, index) => index === groupIndex ? { ...group, filters: [...group.filters, { key: this.configuredColumns()[0]?.key ?? '', operator: 'contains', value: '' }] } : group)); }
  removeFilter(groupIndex: number, filterIndex: number): void { this.localFilterGroups.update(groups => groups.map((group, index) => index === groupIndex ? { ...group, filters: group.filters.filter((_, current) => current !== filterIndex) } : group).filter(group => group.filters.length)); }
  updateFilter(groupIndex: number, filterIndex: number, field: 'key' | 'operator' | 'value', event: Event): void {
    const value = (event.target as HTMLInputElement | HTMLSelectElement).value;
    this.localFilterGroups.update(groups => groups.map((group, index) => index !== groupIndex ? group : { ...group, filters: group.filters.map((filter, current) => current !== filterIndex ? filter : { ...filter, [field]: field === 'operator' ? value as HisHopeFilterOperator : value }) }));
  }
  applyFilters(): void { this.filterChange.emit(this.localFilterGroups()); this.queryChange.emit({ ...this.query(), page: 1, cursor: undefined, filters: Object.fromEntries(this.localFilterGroups().flatMap(group => group.filters.filter(filter => filter.key && filter.value !== '').map(filter => [filter.key, filter.value]))) }); }
  canRunAction(action: HisHopeBulkAction): boolean { return this.permissionChecker()(action.permission); }
  saveView(): void {
    const view = { hiddenColumns: this.localHiddenColumns(), columnOrder: this.localColumnOrder(), columnWidths: this.localColumnWidths(), sort: this.sort() ?? this.localSort() };
    if (this.serverBackedView()) {
      this.viewSaveRequested.emit({ name: this.viewName(), payload: view });
      this.viewChange.emit(view);
      return;
    }
    const key = this.viewStorageKey();
    if (!key || typeof localStorage === 'undefined') return;
    localStorage.setItem(key, JSON.stringify(view));
    this.viewChange.emit(view);
  }
  resetView(): void {
    const key = this.viewStorageKey();
    if (key && typeof localStorage !== 'undefined') localStorage.removeItem(key);
    if (this.serverBackedView()) this.viewResetRequested.emit({ name: this.viewName() });
    this.localHiddenColumns.set([]); this.localColumnOrder.set([]); this.localColumnWidths.set({}); this.localSort.set(null); this.viewChange.emit({ hiddenColumns: [], columnOrder: [], columnWidths: {}, sort: null });
  }
  private restoreSavedView(key: string): void {
    this.restoredViewKey = key;
    if (typeof localStorage === 'undefined') return;
    try {
      const saved = JSON.parse(localStorage.getItem(key) ?? 'null') as { hiddenColumns?: string[]; columnOrder?: string[]; columnWidths?: Record<string, number>; sort?: HisHopeSort | null } | null;
      if (!saved) return;
      this.applySavedView(saved);
    } catch { /* Ignore a corrupted preference and keep the default view. */ }
  }
  private applySavedView(saved: { hiddenColumns?: string[]; columnOrder?: string[]; columnWidths?: Record<string, number>; sort?: HisHopeSort | null }): void {
    this.localHiddenColumns.set(saved.hiddenColumns ?? []); this.localColumnOrder.set(saved.columnOrder ?? []); this.localColumnWidths.set(saved.columnWidths ?? {}); this.localSort.set(saved.sort ?? null);
  }
  onCellKeydown(event: KeyboardEvent, rowIndex: number, columnIndex: number): void {
    if (!this.keyboardNavigation()) return;
    const direction: Record<string, [number, number]> = { ArrowUp: [-1, 0], ArrowDown: [1, 0], ArrowLeft: [0, -1], ArrowRight: [0, 1] };
    const delta = direction[event.key];
    if (!delta) return;
    const table = (event.currentTarget as HTMLElement).closest('table');
    const rows = table ? Array.from(table.querySelectorAll('tbody tr:not([aria-hidden="true"])')) as HTMLTableRowElement[] : [];
    const nextRow = rows[rowIndex + delta[0]];
    const nextCell = nextRow ? Array.from(nextRow.querySelectorAll('td'))[columnIndex + delta[1]] as HTMLElement | undefined : undefined;
    if (nextCell) { event.preventDefault(); nextCell.focus(); }
  }
  copyTable(): void {
    const columns = this.visibleColumns().filter(column => column.exportable !== false);
    const text = [columns.map(column => column.label).join('\t'), ...this.rows().map(row => columns.map(column => this.cellValue(row, column.key)).join('\t'))].join('\n');
    void navigator.clipboard?.writeText(text);
  }
  printTable(): void { if (typeof window !== 'undefined') window.print(); }
  onImportFile(event: Event): void { const file = (event.target as HTMLInputElement).files?.[0]; if (!file) return; const format = file.name.toLowerCase().endsWith('.xlsx') ? 'xlsx' : 'csv'; this.importRequested.emit({ format, file, columns: this.visibleColumns().map(column => column.key) }); (event.target as HTMLInputElement).value = ''; }
  requestAnalysis(operation: 'pivot' | 'aggregate' | 'formula'): void { this.analysisRequested.emit({ operation, query: this.query(), columns: this.visibleColumns().map(column => column.key) }); }
  totalRows(): number { return this.mode() === 'server' && this.totalItems() !== null ? this.totalItems()! : this.rows().length; }
  sortStates(): HisHopeDataTableSort[] { const value = this.sort() ?? this.localSort(); return !value ? [] : Array.isArray(value) ? value : [value]; }
  sortState(): HisHopeDataTableSort | null { return this.sortStates()[0] ?? null; }
  pageValue(): number { return this.page() > 1 ? this.page() : this.localPage(); }
  pageSizeValue(): number { return this.pageSize() !== 10 ? this.pageSize() : this.localPageSize(); }
  pageCount(): number { return Math.max(1, Math.ceil(this.totalRows() / Math.max(1, this.pageSizeValue()))); }
  cursorPaging(): boolean { return !!this.query().cursor || this.nextCursor() !== null || this.previousCursor() !== null; }
  changeCursor(cursor: string | null): void { this.queryChange.emit(createHisHopeCursorQuery(this.query(), cursor)); }
  pageStart(): number { return this.totalRows() ? ((this.pageValue() - 1) * this.pageSizeValue()) + 1 : 0; }
  pageEnd(): number { return Math.min(this.totalRows(), this.pageValue() * this.pageSizeValue()); }
  sortedRows(): Record<string, unknown>[] { const active = this.sortStates(); if (this.mode() === 'server' || !active.length) return this.rows(); return [...this.rows()].sort((a, b) => { for (const term of active) { const result = String(a[term.key] ?? '').localeCompare(String(b[term.key] ?? ''), undefined, { numeric: true, sensitivity: 'base' }); if (result) return result * (term.direction === 'asc' ? 1 : -1); } return 0; }); }
  pagedRows(): Record<string, unknown>[] { const rows = this.flattenTree(this.sortedRows()); if (this.mode() === 'server' || this.virtualize()) return rows; return rows.slice((this.pageValue() - 1) * this.pageSizeValue(), this.pageValue() * this.pageSizeValue()); }
  private treeDepths = new Map<string, number>();
  treeDepth(row: Record<string, unknown>): number { return this.treeDepths.get(this.rowKeyValue(row)) ?? 0; }
  private flattenTree(rows: Record<string, unknown>[]): Record<string, unknown>[] {
    const key = this.treeChildrenKey();
    if (!key) return rows;
    this.treeDepths.clear();
    const output: Record<string, unknown>[] = [];
    const append = (items: Record<string, unknown>[], depth: number) => items.forEach(item => { this.treeDepths.set(this.rowKeyValue(item), depth); output.push(item); const children = item[key]; if (Array.isArray(children)) append(children.filter((child): child is Record<string, unknown> => !!child && typeof child === 'object'), depth + 1); });
    append(rows, 0);
    return output;
  }
  virtualEnd(): number { return Math.min(this.pagedRows().length, this.virtualStart() + this.virtualWindowSize()); }
  virtualWindowSize(): number { return Math.max(1, Math.ceil(560 / Math.max(24, this.virtualRowHeight())) + 4); }
  renderedRows(): Record<string, unknown>[] { const rows = this.pagedRows(); return this.virtualize() ? rows.slice(this.virtualStart(), this.virtualEnd()) : rows; }
  onVirtualScroll(event: Event): void {
    const target = event.target as HTMLElement;
    if (this.virtualizeColumns()) {
      this.columnVirtualViewport.set(Math.max(320, target.clientWidth));
      this.columnVirtualStart.set(Math.max(0, Math.floor(target.scrollLeft / 160) - 2));
    }
    if (!this.virtualize()) return;
    const next = Math.max(0, Math.min(Math.floor(target.scrollTop / Math.max(24, this.virtualRowHeight())), Math.max(0, this.pagedRows().length - this.virtualWindowSize())));
    if (next !== this.virtualStart()) this.virtualStart.set(next);
  }
  rowKeyValue(row: Record<string, unknown>): string { return String(row['id'] ?? row['key'] ?? JSON.stringify(row)); }
  cellValue(row: Record<string, unknown>, key: string): string { const column = this.columns().find(item => item.key === key); if (column?.computed) return String(column.computed(row) ?? ''); return String(key.split('.').reduce<unknown>((value, part) => value && typeof value === 'object' ? (value as Record<string, unknown>)[part] : undefined, row) ?? ''); }
  cellTemplate(key: string): HisHopeDataTableCellDirective | undefined { return this.cellTemplates?.find(template => template.key() === key); }
  detailTemplate(): HisHopeDataTableDetailDirective | null { return this.detailTemplates?.first ?? null; }
  isExpanded(row: Record<string, unknown>): boolean { return this.expandedRowKeys().includes(this.rowKeyValue(row)); }
  toggleExpanded(row: Record<string, unknown>): void { const rowKey = this.rowKeyValue(row); this.rowExpandChange.emit({ rowKey, expanded: !this.isExpanded(row) }); }
  isColumnVisible(key: string): boolean { return this.visibleColumns().some(column => column.key === key); }
  toggleColumn(key: string): void {
    const column = this.columns().find(item => item.key === key);
    if (column && this.isResponsiveHidden(column)) {
      this.responsiveVisibleColumns.update(current => current.includes(key) ? current.filter(item => item !== key) : [...current, key]);
      return;
    }
    const hidden = this.localHiddenColumns().length ? this.localHiddenColumns() : this.hiddenColumns();
    this.localHiddenColumns.set(hidden.includes(key) ? hidden.filter(item => item !== key) : [...hidden, key]);
  }
  private isResponsiveHidden(column: HisHopeDataTableColumn): boolean {
    const priority = column.responsivePriority;
    if (!priority) return false;
    return this.viewportWidth() <= 600 ? priority >= 2 : this.viewportWidth() <= 1024 && priority >= 3;
  }
  beginColumnDrag(key: string): void { this.draggingColumnKey = key; }
  dropColumn(targetKey: string): void {
    const sourceKey = this.draggingColumnKey;
    this.draggingColumnKey = null;
    if (!sourceKey || sourceKey === targetKey) return;
    const configuredOrder = this.localColumnOrder().length ? this.localColumnOrder() : this.columnOrder();
    const order = configuredOrder.length ? [...configuredOrder] : this.columns().map(column => column.key);
    const sourceIndex = order.indexOf(sourceKey); const targetIndex = order.indexOf(targetKey);
    if (sourceIndex < 0 || targetIndex < 0) return;
    order.splice(sourceIndex, 1); order.splice(targetIndex, 0, sourceKey); this.localColumnOrder.set(order); this.columnOrderChange.emit(order);
  }
  moveColumn(key: string, delta: number): void {
    const configuredOrder = this.localColumnOrder().length ? this.localColumnOrder() : this.columnOrder();
    const order = configuredOrder.length ? [...configuredOrder] : this.columns().map(column => column.key);
    const index = order.indexOf(key); const target = index + delta;
    if (index < 0 || target < 0 || target >= order.length) return;
    [order[index], order[target]] = [order[target], order[index]];
    this.localColumnOrder.set(order); this.columnOrderChange.emit(order);
  }
  beginColumnResize(event: PointerEvent, column: HisHopeDataTableColumn): void { event.preventDefault(); event.stopPropagation(); this.resizingColumnKey = column.key; this.resizeStartX = event.clientX; this.resizeStartWidth = this.columnWidth(column) ?? 160; }
  @HostListener('document:pointermove', ['$event']) onColumnResize(event: PointerEvent): void { if (!this.resizingColumnKey) return; const column = this.columns().find(item => item.key === this.resizingColumnKey); if (!column) return; const width = this.clampColumnWidth(this.resizeStartWidth + event.clientX - this.resizeStartX, column); this.localColumnWidths.update(widths => ({ ...widths, [column.key]: width })); }
  @HostListener('document:pointerup') endColumnResize(): void { if (!this.resizingColumnKey) return; const key = this.resizingColumnKey; const width = this.localColumnWidths()[key]; this.resizingColumnKey = null; if (width) this.columnResizeChange.emit({ key, width }); }
  resizeByKeyboard(event: KeyboardEvent, column: HisHopeDataTableColumn, delta: number): void { event.preventDefault(); const width = this.clampColumnWidth((this.columnWidth(column) ?? 160) + delta, column); this.localColumnWidths.update(widths => ({ ...widths, [column.key]: width })); this.columnResizeChange.emit({ key: column.key, width }); }
  private clampColumnWidth(width: number, column: HisHopeDataTableColumn): number { return Math.max(column.minWidth ?? 96, Math.min(column.maxWidth ?? 640, Math.round(width))); }
  exportFormat(): HisHopeTableExportFormat { return this.exportFormats().includes(this.localExportFormat()) ? this.localExportFormat() : (this.exportFormats()[0] ?? 'csv'); }
  changeExportFormat(event: Event): void { this.localExportFormat.set((event.target as HTMLSelectElement).value as HisHopeTableExportFormat); }
  requestExport(): void { const selected = this.selectedRowsValue(); const rows = selected.length ? selected : this.rows(); this.exportRequested.emit({ format: this.exportFormat(), columns: this.visibleColumns().filter(column => column.exportable !== false).map(column => column.key), rowKeys: rows.map(row => this.rowKeyValue(row)), query: this.query(), async: this.mode() === 'server' || this.rows().length > 1000, maskSensitive: this.maskSensitiveExport() }); }
  completedRows(job: HisHopeBulkJob): number { return job.rowProgress?.filter(item => item.status === 'completed').length ?? job.processed; }
  sortBy(key: string, event?: Event): void { const next = toggleHisHopeDataTableSort(this.sortStates(), key, event instanceof MouseEvent && event.shiftKey); this.localSort.set(next); this.localPage.set(1); this.sortChange.emit(next); this.emitQuery(1, this.pageSizeValue(), next); }
  sortIcon(key: string): string { const active = this.sortStates().find(item => item.key === key); if (!active) return 'unfold_more'; return active.direction === 'asc' ? 'arrow_upward' : 'arrow_downward'; }
  sortPriority(key: string): number | null { const index = this.sortStates().findIndex(item => item.key === key); return index < 0 ? null : index + 1; }
  sortLabel(column: HisHopeDataTableColumn): string {
    const index = this.sortStates().findIndex(item => item.key === column.key);
    if (index < 0) return `Sort by ${column.label} ascending. Hold Shift to add a sort level.`;
    const direction = this.sortStates()[index].direction;
    return `${column.label} sorted ${direction}, priority ${index + 1}. Click to reverse; hold Shift to preserve other sort levels.`;
  }
  changePage(page: number): void { this.localPage.set(Math.max(1, Math.min(page, this.pageCount()))); this.pageChange.emit(this.localPage()); this.emitQuery(this.localPage(), this.pageSizeValue()); }
  changePageSize(event: Event): void { const size = Number((event.target as HTMLSelectElement).value); this.localPageSize.set(size); this.localPage.set(1); this.pageSizeChange.emit(size); this.pageChange.emit(1); this.emitQuery(1, size); }
  onRowClick(row: Record<string, unknown>): void { if (this.rowClickable()) this.rowClick.emit(row); }
  onMobileRowClick(row: Record<string, unknown>): void { if (this.detailTemplate()) { this.toggleExpanded(row); return; } this.onRowClick(row); }
  displayValue(row: Record<string, unknown>, key: string): string { return String(this.cellValue(row, key) ?? ''); }
  isEditing(row: Record<string, unknown>): boolean { return this.editingRowKey() === this.rowKeyValue(row); }
  isSaving(row: Record<string, unknown>): boolean { return this.savingRowKey() === this.rowKeyValue(row) || (this.editState()?.rowKey === this.rowKeyValue(row) && this.editState()?.status === 'saving'); }
  isDirty(row: Record<string, unknown>): boolean { return this.editState()?.rowKey === this.rowKeyValue(row) ? this.editState()?.dirty === true : JSON.stringify(this.draftRow()) !== JSON.stringify(row) && this.isEditing(row); }
  editErrorMessage(): string { return this.editState()?.rowKey === this.editingRowKey() ? this.editState()?.error ?? '' : ''; }
  draftValue(key: string): string { return String(this.draftRow()[key] ?? ''); }
  editError(key: string): string { return this.draftErrors()[key] ?? ''; }
  hasEditErrors(): boolean { return Object.keys(this.draftErrors()).length > 0; }
  startEdit(row: Record<string, unknown>): void { this.editingRowKey.set(this.rowKeyValue(row)); this.draftRow.set({ ...row }); this.draftErrors.set({}); this.previousEditRow = { ...row }; }
  updateDraftValue(key: string, value: unknown): void {
    const next = { ...this.draftRow(), [key]: value };
    const column = this.columns().find(item => item.key === key);
    const message = column?.editValidator?.(value, next) ?? null;
    const errors = { ...this.draftErrors() };
    if (message) errors[key] = message; else delete errors[key];
    this.draftRow.set(next); this.draftErrors.set(errors);
  }
  updateDraft(key: string, event: Event): void { this.updateDraftValue(key, (event.target as HTMLInputElement | HTMLSelectElement).value); }
  saveEdit(row: Record<string, unknown>): void {
    if (this.hasEditErrors() || this.savingRowKey() === this.rowKeyValue(row)) return;
    const current = { ...row, ...this.draftRow() };
    const previous = this.previousEditRow ?? row;
    const request = { rowKey: this.rowKeyValue(row), previous, current, concurrencyToken: String(row['concurrencyToken'] ?? row['version'] ?? '') || undefined };
    this.lastSaveRequest = request;
    this.lastEdit.set({ previous, current });
    this.rowEditSave.emit(current); this.rowEditSaveRequested.emit(request);
    if (!this.editState()) { this.editingRowKey.set(null); this.draftRow.set({}); this.draftErrors.set({}); this.previousEditRow = null; }
    else this.previousEditRow = null;
  }
  retryEditSave(): void {
    if (!this.lastSaveRequest) return;
    const current = this.editingRowKey() === this.lastSaveRequest.rowKey ? { ...this.lastSaveRequest.current, ...this.draftRow() } : this.lastSaveRequest.current;
    this.lastSaveRequest = { ...this.lastSaveRequest, current };
    this.rowEditSaveRequested.emit(this.lastSaveRequest);
  }
  undoEdit(transaction: { previous: Record<string, unknown>; current: Record<string, unknown> }): void { this.rowEditUndo.emit(transaction); this.lastEdit.set(null); }
  resolveConflict(resolution: HisHopeConflictResolution): void {
    const conflict = this.editConflict();
    if (!conflict) return;
    const submitted = { ...conflict.submitted };
    if (resolution === 'latest') {
      this.draftRow.set({ ...conflict.latest });
      this.draftErrors.set({});
      this.previousEditRow = { ...conflict.latest };
    } else if (resolution === 'mine') {
      this.draftRow.set(submitted);
      this.previousEditRow = { ...conflict.latest };
    } else {
      const fields = conflict.mergeableFields ?? Object.keys(submitted);
      const merged = fields.reduce((result, key) => ({ ...result, [key]: submitted[key] ?? conflict.latest[key] }), { ...conflict.latest });
      this.draftRow.set(merged);
      this.previousEditRow = { ...conflict.latest };
    }
    this.conflictResolved.emit({ resolution, conflict });
    if (resolution === 'mine' || resolution === 'merge') {
      const current = resolution === 'merge' ? this.draftRow() : submitted;
      this.lastSaveRequest = { rowKey: conflict.rowKey, previous: { ...conflict.latest }, current, concurrencyToken: conflict.latestConcurrencyToken };
      this.rowEditSaveRequested.emit(this.lastSaveRequest);
    }
  }
  cancelEdit(): void { this.editingRowKey.set(null); this.draftRow.set({}); this.draftErrors.set({}); this.previousEditRow = null; this.rowEditCancel.emit(); }
  isSelected(row: Record<string, unknown>): boolean { return this.selectedRows.has(this.rowKeyValue(row)); }
  allVisibleSelected(): boolean { return this.pagedRows().length > 0 && (this.selectionScope() === 'all' && this.selectAllPages ? this.pagedRows().every(row => !this.excludedRows.has(this.rowKeyValue(row))) : this.pagedRows().every(row => this.isSelected(row))); }
  toggleRow(row: Record<string, unknown>): void { const key = this.rowKeyValue(row); if (this.selectionScope() === 'all' && this.selectAllPages) { this.excludedRows.has(key) ? this.excludedRows.delete(key) : this.excludedRows.add(key); } else { this.isSelected(row) ? this.selectedRows.delete(key) : this.selectedRows.add(key); } this.emitSelection(); this.emitSelectionState(); }
  toggleAll(): void { if (this.selectionScope() === 'all') { if (this.allVisibleSelected()) { this.selectAllPages = false; this.excludedRows.clear(); this.selectedRows.clear(); } else { this.selectAllPages = true; this.excludedRows.clear(); this.selectedRows.clear(); } } else { this.allVisibleSelected() ? this.pagedRows().forEach(row => this.selectedRows.delete(this.rowKeyValue(row))) : this.pagedRows().forEach(row => this.selectedRows.add(this.rowKeyValue(row))); } this.emitSelection(); this.emitSelectionState(); }
  selectedRowsValue(): Record<string, unknown>[] { return this.rows().filter(row => this.selectionScope() === 'all' && this.selectAllPages ? !this.excludedRows.has(this.rowKeyValue(row)) : this.selectedRows.has(this.rowKeyValue(row))); }
  hasSelection(): boolean { return this.selectAllPages || this.selectedRows.size > 0; }
  selectionCount(): string | number { return this.selectAllPages ? 'all' : this.selectedRows.size; }
  runBulkAction(action: HisHopeBulkAction): void { const rows = this.selectedRowsValue(); action.execute?.(rows); this.bulkActionRequested.emit({ actionId: action.id, rowKeys: rows.map(row => this.rowKeyValue(row)), query: this.query(), selection: this.selectionStateValue() }); }
  private emitQuery(page: number, pageSize: number, sort: HisHopeSort | null = this.sort() ?? this.localSort()): void { this.queryChange.emit({ ...this.query(), page, pageSize, sort: sort ?? undefined, cursor: undefined }); }
  private emitSelection(): void { this.selectionChange.emit(this.rows().filter(row => this.selectedRows.has(this.rowKeyValue(row)))); }
  private selectionStateValue(): HisHopeTableSelectionState { return { scope: this.selectionScope(), selectedKeys: this.selectAllPages ? [] : [...this.selectedRows], excludedKeys: [...this.excludedRows], query: this.query() }; }
  private emitSelectionState(): void { this.selectionStateChange.emit(this.selectionStateValue()); }
}
