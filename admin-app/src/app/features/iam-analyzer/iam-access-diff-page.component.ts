import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  OnInit,
  effect,
  inject,
} from "@angular/core";
import { CommonModule } from "@angular/common";
import { FormControl, FormGroup, ReactiveFormsModule } from "@angular/forms";
import { catchError, of } from "rxjs";
import {
  HisHopePageHeaderComponent,
  HisHopePageLayoutComponent,
  HisHopeTableStateComponent,
  HisHopeToolbarComponent,
} from "@his-hope/frontend-foundation/ui";
import {
  HisHopeI18nService,
  HisHopeTranslatePipe,
} from "@his-hope/frontend-foundation/i18n";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { IamPermissionSet } from "../../core/contracts/admin.contracts";
import { IamApiService } from "../../core/services/iam-api.service";
import { AdminResourceStateController } from "../../core/services/admin-resource-state.controller";
import { TenantContextService } from "../../core/services/tenant-context.service";

import { HisHopeActionButtonComponent } from "@his-hope/frontend-foundation/ui";
@Component({
  selector: "app-iam-access-diff-page",
  standalone: true,
  imports: [
    HisHopeActionButtonComponent,
    CommonModule,
    ReactiveFormsModule,
    HisHopePageHeaderComponent,
    HisHopePageLayoutComponent,
    HisHopeTableStateComponent,
    HisHopeToolbarComponent,
    HisHopeTranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<hh-page-layout
    ><hh-page-header
      hhPageHeader
      [title]="'admin.newAccessDiff' | hhTranslate: 'New-access diff'"
      [subtitle]="
        'admin.newAccessDiffSubtitle'
          | hhTranslate: 'Compare permission sets before and after a change.'
      "
    /><hh-toolbar hhPageToolbar [label]="'admin.newAccessDiff' | hhTranslate"
      ><hh-action-button
        (pressed)="load()"
        hh-toolbar-actions
        kind="secondary"
        icon="refresh"
        [label]="'admin.refresh' | hhTranslate"
    /></hh-toolbar>
    <form [formGroup]="formGroup" class="hh-form-grid">
      <label
        >{{ "admin.before" | hhTranslate
        }}<select
          [formControl]="formGroup.controls.beforeId"
          [disabled]="loadingSets || !sets.length"
        >
          <option *ngFor="let set of sets" [value]="set.id">
            {{ set.displayName }} · {{ set.key }}
          </option>
        </select></label
      ><label
        >{{ "admin.after" | hhTranslate
        }}<select
          [formControl]="formGroup.controls.afterId"
          [disabled]="loadingSets || !sets.length"
        >
          <option *ngFor="let set of sets" [value]="set.id">
            {{ set.displayName }} · {{ set.key }}
          </option>
        </select></label
      >
    </form>
    <hh-action-button
      [disabled]="
        loadingSets ||
        !formGroup.controls.beforeId.value ||
        !formGroup.controls.afterId.value
      "
      (pressed)="compare()"
      kind="primary"
      icon="compare_arrows"
      [label]="'admin.compare' | hhTranslate: 'Compare'"
    /><hh-table-state
      *ngIf="loadingSets"
      kind="loading"
      message="admin.loading"
    /><hh-table-state
      *ngIf="!loadingSets && !error && !sets.length"
      kind="empty"
      message="admin.noPermissionSets"
    /><hh-table-state
      *ngIf="error"
      kind="error"
      message="admin.iamAnalyzerFailed"
      ><hh-action-button
        kind="secondary"
        icon="refresh"
        [label]="'admin.retry' | hhTranslate: 'Retry'"
        (pressed)="load()"
    /></hh-table-state>
    <pre *ngIf="result">{{ result | json }}</pre>
  </hh-page-layout>`,
})
export class IamAccessDiffPageComponent implements OnInit {
  private readonly api = inject(IamApiService);
  private readonly tenantContext = inject(TenantContextService);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);
  sets: IamPermissionSet[] = [];
  readonly formGroup = new FormGroup({
    beforeId: new FormControl("", { nonNullable: true }),
    afterId: new FormControl("", { nonNullable: true }),
  });
  result: unknown;
  error = "";
  readonly state = new AdminResourceStateController<IamPermissionSet[]>({
    destroyRef: this.destroyRef,
    i18n: this.i18n,
    loadErrorMessageKey: "admin.iamAnalyzerFailed",
    loadErrorFallback: "Analyzer failed.",
  });
  get loadingSets(): boolean {
    return this.state.loading;
  }
  constructor() {
    effect(() => {
      const sets = this.state.resource.data();
      if (sets) {
        this.sets = sets;
        this.formGroup.patchValue({
          beforeId:
            this.formGroup.controls.beforeId.value || (sets[0]?.id ?? ""),
          afterId:
            this.formGroup.controls.afterId.value ||
            sets[1]?.id ||
            sets[0]?.id ||
            "",
        });
        this.cdr.markForCheck();
      }
    });
  }
  ngOnInit(): void {
    this.load();
    this.tenantContext.bindTenantReload(this.destroyRef, () => this.load());
  }
  load(): void {
    this.error = "";
    this.state.load(
      this.api
        .getIamPermissionSets(this.tenantContext.getActiveEnvironmentScopeId())
        .pipe(
          catchError(() => {
            this.error = this.i18n.t(
              "admin.iamAnalyzerFailed",
              "Analyzer failed.",
            );
            return of([]);
          }),
        ),
    );
  }
  private permissions(id: string): string[] {
    const set = this.sets.find((item) => item.id === id);
    try {
      return set ? (JSON.parse(set.permissionsJson) as string[]) : [];
    } catch {
      return [];
    }
  }
  compare(): void {
    const { beforeId, afterId } = this.formGroup.getRawValue();
    if (!beforeId || !afterId) return;
    this.api
      .analyzeIamNewAccessDiff(
        this.permissions(beforeId),
        this.permissions(afterId),
      )
      .subscribe({
        next: (result) => {
          this.result = result;
          this.cdr.markForCheck();
        },
        error: () => {
          this.error = this.i18n.t(
            "admin.iamAnalyzerFailed",
            "Analyzer failed.",
          );
          this.cdr.markForCheck();
        },
      });
  }
}
