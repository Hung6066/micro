import { HttpClient } from "@angular/common/http";
import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  OnInit,
  inject,
} from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import {
  HisHopeActionButtonComponent,
  HisHopeDataTableCellDirective,
  HisHopeDataTableColumn,
  HisHopeDataTableComponent,
  HisHopePageHeaderComponent,
  HisHopePageLayoutComponent,
  HisHopeStateComponent,
  HisHopeToolbarComponent,
} from "@his-hope/frontend-foundation/ui";
import {
  HisHopeI18nService,
  HisHopeTranslatePipe,
} from "@his-hope/frontend-foundation/i18n";
import { environment } from "../../../environments/environment";

interface PortalUser {
  id: string;
  username: string;
  email: string;
  isActive: boolean;
}

@Component({
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    HisHopeActionButtonComponent,
    HisHopeDataTableCellDirective,
    HisHopeDataTableComponent,
    HisHopePageHeaderComponent,
    HisHopePageLayoutComponent,
    HisHopeToolbarComponent,
    HisHopeTranslatePipe,
  ],
  template: `
    <hh-page-layout>
      <hh-page-header
        hhPageHeader
        [title]="'customerPortal.usersTitle' | hhTranslate"
        [subtitle]="'customerPortal.usersSubtitle' | hhTranslate"
      />
      <hh-toolbar hhPageToolbar [label]="'customerPortal.users' | hhTranslate">
        <span hhToolbarTitle
          >{{ users.length }} {{ "customerPortal.users" | hhTranslate }}</span
        >
        <hh-action-button
          hh-toolbar-actions
          kind="secondary"
          icon="refresh"
          [label]="'customerPortal.refresh' | hhTranslate"
          (pressed)="loadUsers()"
        />
      </hh-toolbar>
      <hh-data-table
        [label]="'customerPortal.users' | hhTranslate"
        [columns]="columns"
        [rows]="tableRows"
        [loading]="loading"
        [error]="error"
        mode="client"
        [mobilePresentation]="'list'"
        [empty]="!loading && !error && users.length === 0"
        [emptyMessage]="'admin.noUsers' | hhTranslate: 'No users found'"
        (retry)="loadUsers()"
      >
        <ng-template hhDataTableCell="isActive" let-row>
          {{
            row.isActive
              ? ("admin.active" | hhTranslate)
              : ("admin.inactive" | hhTranslate)
          }}
        </ng-template>
      </hh-data-table>
    </hh-page-layout>
  `,
})
export class UsersPageComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);
  private readonly i18n = inject(HisHopeI18nService);

  users: PortalUser[] = [];
  loading = true;
  error = "";

  get columns(): HisHopeDataTableColumn[] {
    this.i18n.locale();
    return [
      {
        key: "username",
        label: this.i18n.t("admin.username", "Username"),
        sortable: true,
      },
      {
        key: "email",
        label: this.i18n.t("admin.email", "Email"),
        sortable: true,
      },
      {
        key: "isActive",
        label: this.i18n.t("admin.active", "Active"),
        sortable: true,
        status: true,
      },
    ];
  }

  get tableRows(): Record<string, unknown>[] {
    return this.users as unknown as Record<string, unknown>[];
  }

  ngOnInit(): void {
    this.loadUsers();
  }

  loadUsers(): void {
    this.loading = true;
    this.error = "";
    this.http
      .get<{ items: PortalUser[] }>(
        `${environment.adminApiUrl}/users?page=1&pageSize=20`,
      )
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (result) => {
          this.users = result.items ?? [];
          this.loading = false;
          this.cdr.markForCheck();
        },
        error: () => {
          this.users = [];
          this.loading = false;
          this.error = this.i18n.t(
            "customerPortal.usersLoadFailed",
            "Unable to load users.",
          );
          this.cdr.markForCheck();
        },
      });
  }
}
