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
import { User } from "../../core/contracts/admin.contracts";
import { IamApiService } from "../../core/services/iam-api.service";
import { AdminResourceStateController } from "../../core/services/admin-resource-state.controller";

@Component({
  selector: "app-iam-effective-access-page",
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
      [title]="'admin.effectiveAccess' | hhTranslate: 'Effective access'"
      [subtitle]="
        'admin.effectiveAccessSubtitle'
          | hhTranslate
            : 'Resolve a principal’s effective permissions from assignments and boundaries.'
      "
    /><hh-toolbar hhPageToolbar [label]="'admin.effectiveAccess' | hhTranslate"
      ><button
        hhToolbarActions
        type="button"
        class="hh-button hh-button--secondary"
        (click)="load()"
      >
        {{ "admin.refresh" | hhTranslate }}
      </button></hh-toolbar
    ><label class="hh-field"
      >{{ "admin.subject" | hhTranslate
      }}<select
        [(ngModel)]="selectedUserId"
        [disabled]="loadingUsers || !users.length"
      >
        <option *ngIf="loadingUsers" value="">
          {{ "admin.loading" | hhTranslate: "Loading…" }}
        </option>
        <option *ngIf="!loadingUsers && !users.length" value="">
          {{ "admin.noUsers" | hhTranslate: "No users available" }}
        </option>
        <option *ngFor="let user of users" [value]="user.id">
          {{ user.email || user.userName }}
        </option>
      </select></label
    ><button
      type="button"
      class="hh-button hh-button--primary"
      (click)="evaluate()"
      [disabled]="loadingUsers || !selectedUserId"
    >
      {{ "admin.evaluate" | hhTranslate: "Evaluate" }}</button
    ><hh-table-state
      *ngIf="loadingUsers"
      kind="loading"
      message="admin.loading"
    /><hh-table-state
      *ngIf="!loadingUsers && !error && !users.length"
      kind="empty"
      message="admin.noUsers"
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
export class IamEffectiveAccessPageComponent implements OnInit {
  private readonly api = inject(IamApiService);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);
  users: User[] = [];
  selectedUserId = "";
  result: unknown;
  error = "";
  readonly state = new AdminResourceStateController<User[]>({
    destroyRef: this.destroyRef,
    i18n: this.i18n,
    loadErrorMessageKey: "admin.iamAnalyzerFailed",
    loadErrorFallback: "Analyzer failed.",
  });
  get loadingUsers(): boolean {
    return this.state.loading;
  }
  constructor() {
    effect(() => {
      const users = this.state.resource.data();
      if (users) {
        this.users = users;
        this.selectedUserId ||= users[0]?.id ?? "";
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
      this.api.getUsers().pipe(
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
  evaluate(): void {
    if (!this.selectedUserId) return;
    this.api.getIamEffectiveAccess(this.selectedUserId).subscribe({
      next: (result) => {
        this.result = result;
        this.cdr.markForCheck();
      },
      error: () => {
        this.error = this.i18n.t("admin.iamAnalyzerFailed", "Analyzer failed.");
        this.cdr.markForCheck();
      },
    });
  }
}
