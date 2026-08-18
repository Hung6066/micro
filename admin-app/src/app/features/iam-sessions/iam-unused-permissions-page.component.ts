import {
  ChangeDetectorRef,
  Component,
  DestroyRef,
  OnInit,
  effect,
  inject,
} from "@angular/core";
import { CommonModule } from "@angular/common";
import { catchError, of } from "rxjs";
import {
  HisHopeDataTableComponent,
  HisHopeDataTableColumn,
  HisHopePageHeaderComponent,
  HisHopePageLayoutComponent,
  HisHopeToolbarComponent,
} from "@his-hope/frontend-foundation/ui";
import {
  HisHopeI18nService,
  HisHopeTranslatePipe,
} from "@his-hope/frontend-foundation/i18n";
import { IamApiService } from "../../core/services/iam-api.service";
import { AdminResourceStateController } from "../../core/services/admin-resource-state.controller";

@Component({
  selector: "app-iam-unused-permissions-page",
  standalone: true,
  imports: [
    CommonModule,
    HisHopeDataTableComponent,
    HisHopePageHeaderComponent,
    HisHopePageLayoutComponent,
    HisHopeToolbarComponent,
    HisHopeTranslatePipe,
  ],
  template: `<hh-page-layout
    ><hh-page-header
      hhPageHeader
      [title]="'admin.unusedPermissions' | hhTranslate: 'Unused permissions'"
      [subtitle]="
        'admin.unusedPermissionsSubtitle'
          | hhTranslate: 'Analyze permissions with no observed usage.'
      " /><hh-toolbar
      hhPageToolbar
      [label]="'admin.unusedPermissions' | hhTranslate"
      ><button
        hhToolbarActions
        type="button"
        class="hh-button hh-button--secondary"
        (click)="load()"
      >
        {{ "admin.refresh" | hhTranslate }}
      </button></hh-toolbar
    >
    <div *ngIf="error" class="hh-state hh-state--error">{{ error }}</div>
    <hh-data-table
      [label]="'admin.unusedPermissions' | hhTranslate"
      [columns]="columns"
      [rows]="rows"
      [loading]="loading"
      [empty]="!loading && !rows.length"
    ></hh-data-table
  ></hh-page-layout>`,
})
export class IamUnusedPermissionsPageComponent implements OnInit {
  private readonly api = inject(IamApiService);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly cdr = inject(ChangeDetectorRef);
  rows: Record<string, unknown>[] = [];
  private readonly destroyRef = inject(DestroyRef);
  readonly state = new AdminResourceStateController<{
    unusedPermissions: string[];
  }>({
    destroyRef: this.destroyRef,
    i18n: this.i18n,
    loadErrorMessageKey: "admin.iamAnalyzerFailed",
    loadErrorFallback: "Analyzer failed.",
  });
  get loading(): boolean {
    return this.state.loading;
  }
  get error(): string {
    return this.state.error;
  }
  set error(value: string) {
    this.state.setActionError(value);
  }
  constructor() {
    effect(() => {
      const data = this.state.resource.data();
      if (data) {
        this.rows = data.unusedPermissions.map((permission) => ({
          permission,
        }));
        this.cdr.markForCheck();
      }
    });
  }
  get columns(): HisHopeDataTableColumn[] {
    return [
      {
        key: "permission",
        label: this.i18n.t("admin.permission", "Permission"),
      },
    ];
  }
  ngOnInit(): void {
    this.load();
  }
  load(): void {
    this.error = "";
    this.state.load(
      this.api.analyzeIamUnusedPermissions().pipe(
        catchError(() => {
          this.error = this.i18n.t(
            "admin.iamAnalyzerFailed",
            "Analyzer failed.",
          );
          return of({ unusedPermissions: [] });
        }),
      ),
    );
  }
}
