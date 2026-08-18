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
import { catchError, forkJoin, of } from "rxjs";
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
import {
  PermissionDefinition,
  User,
} from "../../core/contracts/admin.contracts";
import { IamApiService } from "../../core/services/iam-api.service";
import { AdminResourceStateController } from "../../core/services/admin-resource-state.controller";

import { HisHopeActionButtonComponent } from "@his-hope/frontend-foundation/ui";
@Component({
  selector: "app-iam-policy-simulator-page",
  standalone: true,
  imports: [
    HisHopeActionButtonComponent,
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
      [title]="'admin.policySimulator' | hhTranslate: 'Policy simulator'"
      [subtitle]="
        'admin.policySimulatorSubtitle'
          | hhTranslate
            : 'Test a permission decision before changing production assignments.'
      "
    /><hh-toolbar hhPageToolbar [label]="'admin.policySimulator' | hhTranslate"
      ><hh-action-button
        (pressed)="load()"
        hh-toolbar-actions
        kind="secondary"
        icon="refresh"
        [label]="'admin.refresh' | hhTranslate"
    /></hh-toolbar>
    <div class="hh-form-grid">
      <label
        >{{ "admin.subject" | hhTranslate
        }}<select
          [(ngModel)]="userId"
          [disabled]="loadingUsers || !users.length"
        >
          <option *ngIf="loadingUsers" value="">
            {{ "admin.loading" | hhTranslate: "Loading…" }}
          </option>
          <option *ngFor="let user of users" [value]="user.id">
            {{ user.email || user.userName }}
          </option>
        </select></label
      ><label
        >{{ "admin.permission" | hhTranslate
        }}<select
          [(ngModel)]="permission"
          [disabled]="loadingUsers || !permissions.length"
        >
          <option *ngIf="loadingUsers" value="">
            {{ "admin.loading" | hhTranslate: "Loading…" }}
          </option>
          <option *ngFor="let item of permissions" [value]="item.code">
            {{ item.code }} · {{ item.name }}
          </option>
        </select></label
      >
    </div>
    <hh-action-button
      [disabled]="loadingUsers || !userId || !permission"
      (pressed)="simulate()"
      kind="diagnostic"
      icon="refresh"
      [label]="'admin.simulate' | hhTranslate: 'Simulate'"
    />
    ><hh-table-state
      *ngIf="loadingUsers"
      kind="loading"
      message="admin.loading"
    /><hh-table-state
      *ngIf="!loadingUsers && !error && (!users.length || !permissions.length)"
      kind="empty"
      message="admin.noUsers"
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
export class IamPolicySimulatorPageComponent implements OnInit {
  private readonly api = inject(IamApiService);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);
  users: User[] = [];
  permissions: PermissionDefinition[] = [];
  userId = "";
  permission = "";
  result: unknown;
  error = "";
  readonly state = new AdminResourceStateController<{
    users: User[];
    permissions: PermissionDefinition[];
  }>({
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
      const data = this.state.resource.data();
      if (data) {
        this.users = data.users;
        this.permissions = data.permissions.filter(
          (item) => !item.isDeprecated,
        );
        this.userId ||= data.users[0]?.id ?? "";
        this.permission ||= this.permissions[0]?.code ?? "";
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
      forkJoin({
        users: this.api.getUsers(),
        permissions: this.api.getPermissions(),
      }).pipe(
        catchError(() => {
          this.error = this.i18n.t(
            "admin.iamAnalyzerFailed",
            "Analyzer failed.",
          );
          return of({ users: [], permissions: [] });
        }),
      ),
    );
  }
  simulate(): void {
    if (!this.userId || !this.permission) return;
    this.api
      .simulateIamPolicy({
        userId: this.userId,
        permissionCode: this.permission,
      })
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
