import {
  ChangeDetectionStrategy,
  Component,
  ContentChild,
  Directive,
  TemplateRef,
  input,
  output,
} from "@angular/core";
import { CommonModule } from "@angular/common";
import { HisHopeTranslatePipe } from "@his-hope/frontend-foundation/i18n";
import { HisHopeActionButtonComponent } from "./his-hope-action-button.component";
import {
  HisHopeDataTableCellDirective,
  HisHopeDataTableColumn,
  HisHopeDataTableComponent,
} from "./his-hope-data-table.component";
import { HisHopePageHeaderComponent } from "./his-hope-page-header.component";
import {
  HisHopePageDensity,
  HisHopePageLayoutComponent,
} from "./his-hope-page-layout.component";
import { HisHopeToolbarComponent } from "./his-hope-toolbar.component";

@Directive({
  selector: "[hhResourceRowActions]",
  standalone: true,
})
export class HisHopeResourceRowActionsDirective {
  constructor(
    readonly templateRef: TemplateRef<{ $implicit: Record<string, unknown> }>,
  ) {}
}

/**
 * Composition shell for client-side admin/resource list pages: page chrome,
 * create/refresh toolbar, and table loading/error/empty wiring. Domain pages
 * supply columns, rows, and row-action templates only.
 */
@Component({
  selector: "hh-resource-list-page",
  standalone: true,
  imports: [
    CommonModule,
    HisHopeActionButtonComponent,
    HisHopeDataTableCellDirective,
    HisHopeDataTableComponent,
    HisHopePageHeaderComponent,
    HisHopePageLayoutComponent,
    HisHopeToolbarComponent,
    HisHopeTranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <hh-page-layout [density]="density()">
      <hh-page-header
        hhPageHeader
        [title]="title() | hhTranslate: titleFallback()"
        [subtitle]="subtitle() | hhTranslate: subtitleFallback()"
      />
      <ng-content select="[hhPageHeaderActions]" />
      <ng-content select="[hhResourceListPrefix]" />

      <hh-toolbar
        hhPageToolbar
        [label]="
          (toolbarLabel() || title())
            | hhTranslate: toolbarLabelFallback() || titleFallback()
        "
      >
        @if (count() !== null) {
          <span hhToolbarTitle>
            {{ count() }}
            {{
              (countLabel() || title())
                | hhTranslate: countLabelFallback() || titleFallback()
            }}
          </span>
        }
        @if (showCreate() && canWrite()) {
          <hh-action-button
            hh-toolbar-actions
            kind="primary"
            [icon]="createIcon()"
            [label]="createLabel() | hhTranslate: createLabelFallback()"
            (pressed)="create.emit()"
          />
        }
        @if (showRefresh()) {
          <hh-action-button
            hh-toolbar-actions
            kind="secondary"
            icon="refresh"
            [label]="refreshLabel() | hhTranslate: refreshLabelFallback()"
            (pressed)="refresh.emit()"
          />
        }
        <ng-content select="[hhResourceToolbarActions]" />
      </hh-toolbar>

      <hh-data-table
        [label]="
          (tableLabel() || title())
            | hhTranslate: tableLabelFallback() || titleFallback()
        "
        [columns]="columns()"
        [rows]="rows()"
        [loading]="loading()"
        [error]="error()"
        [empty]="!loading() && !error() && !rows().length"
        [emptyMessage]="emptyMessage() | hhTranslate: emptyMessageFallback()"
        (retry)="refresh.emit()"
      >
        @if (rowActions) {
          <ng-template hhDataTableCell="actions" let-row>
            <ng-container
              *ngTemplateOutlet="rowActions.templateRef; context: { $implicit: row }"
            />
          </ng-template>
        }
        <ng-content />
      </hh-data-table>
    </hh-page-layout>
  `,
})
export class HisHopeResourceListPageComponent {
  readonly density = input<HisHopePageDensity>("comfortable");
  readonly title = input.required<string>();
  readonly titleFallback = input("");
  readonly subtitle = input("");
  readonly subtitleFallback = input("");
  readonly toolbarLabel = input("");
  readonly toolbarLabelFallback = input("");
  readonly count = input<number | null>(null);
  readonly countLabel = input("");
  readonly countLabelFallback = input("");
  readonly tableLabel = input("");
  readonly tableLabelFallback = input("");
  readonly canWrite = input(false);
  readonly showCreate = input(true);
  readonly showRefresh = input(true);
  readonly createLabel = input("admin.create");
  readonly createLabelFallback = input("");
  readonly createIcon = input("add");
  readonly refreshLabel = input("admin.refresh");
  readonly refreshLabelFallback = input("");
  readonly emptyMessage = input("table.empty");
  readonly emptyMessageFallback = input("");
  readonly columns = input.required<HisHopeDataTableColumn[]>();
  readonly rows = input.required<Record<string, unknown>[]>();
  readonly loading = input(false);
  readonly error = input("");

  readonly create = output<void>();
  readonly refresh = output<void>();

  @ContentChild(HisHopeResourceRowActionsDirective)
  rowActions?: HisHopeResourceRowActionsDirective;
}
