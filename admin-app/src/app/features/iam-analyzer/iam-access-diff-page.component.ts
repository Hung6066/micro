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
import { FormsModule } from "@angular/forms";
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
import { IamPermissionSet } from "../../core/contracts/admin.contracts";
import { IamApiService } from "../../core/services/iam-api.service";
import { AdminResourceStateController } from "../../core/services/admin-resource-state.controller";

@Component({
  selector: "app-iam-access-diff-page",
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
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
      ><button
        hhToolbarActions
        type="button"
        class="hh-button hh-button--secondary"
        (click)="load()"
      >
        {{ "admin.refresh" | hhTranslate }}
      </button></hh-toolbar
    >
    <div class="hh-form-grid">
      <label
        >{{ "admin.before" | hhTranslate
        }}<select
          [(ngModel)]="beforeId"
          [disabled]="loadingSets || !sets.length"
        >
          <option *ngFor="let set of sets" [value]="set.id">
            {{ set.key }}
          </option>
        </select></label
      ><label
        >{{ "admin.after" | hhTranslate
        }}<select
          [(ngModel)]="afterId"
          [disabled]="loadingSets || !sets.length"
        >
          <option *ngFor="let set of sets" [value]="set.id">
            {{ set.key }}
          </option>
        </select></label
      >
    </div>
    <button
      type="button"
      class="hh-button hh-button--primary"
      (click)="compare()"
      [disabled]="loadingSets || !beforeId || !afterId"
    >
      {{ "admin.compare" | hhTranslate: "Compare" }}</button
    ><hh-table-state
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
      ><button
        type="button"
        class="hh-button hh-button--secondary"
        (click)="load()"
      >
        {{ "admin.retry" | hhTranslate: "Retry" }}
      </button></hh-table-state
    >
    <pre *ngIf="result">{{ result | json }}</pre>
  </hh-page-layout>`,
})
export class IamAccessDiffPageComponent implements OnInit {
  private readonly api = inject(IamApiService);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);
  sets: IamPermissionSet[] = [];
  beforeId = "";
  afterId = "";
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
        this.beforeId ||= sets[0]?.id ?? "";
        this.afterId ||= sets[1]?.id ?? sets[0]?.id ?? "";
        this.cdr.markForCheck();
      }
    });
  }
  ngOnInit(): void {
    this.load();
  }
  load(): void {
    this.error = "";
    this.state.load(
      this.api.getIamPermissionSets().pipe(
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
    if (!this.beforeId || !this.afterId) return;
    this.api
      .analyzeIamNewAccessDiff(
        this.permissions(this.beforeId),
        this.permissions(this.afterId),
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
