import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  OnInit,
  inject,
} from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { FormControl } from "@angular/forms";
import {
  HIS_HOPE_DIALOG_DATA,
  HisHopeActionButtonComponent,
  HisHopeConfirmDialogComponent,
  HisHopeCreateDialogShellComponent,
  HisHopeDataTableCellDirective,
  HisHopeDataTableColumn,
  HisHopeDataTableComponent,
  HisHopeDialogRef,
  HisHopeToastService,
} from "@his-hope/frontend-foundation/ui";
import { HisHopeMaterialFormFieldComponent } from "@his-hope/frontend-foundation/forms";
import {
  HisHopeI18nService,
  HisHopeTranslatePipe,
} from "@his-hope/frontend-foundation/i18n";
import {
  catchError,
  debounceTime,
  distinctUntilChanged,
  finalize,
  map,
  of,
  startWith,
  switchMap,
} from "rxjs";
import { IamGroup, User } from "../../core/contracts/admin.contracts";
import { IamGroupMember } from "../../core/contracts/iam.contracts";
import { AdminConfirmState } from "../../core/services/admin-confirm-state";
import { IamApiService } from "../../core/services/iam-api.service";
import { UsersApiService } from "../../core/services/users-api.service";

export interface GroupMembersDialogData {
  group: IamGroup;
  canWrite: boolean;
}

const USER_SEARCH_PAGE_SIZE = 20;

@Component({
  selector: "app-group-members-dialog",
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    HisHopeActionButtonComponent,
    HisHopeConfirmDialogComponent,
    HisHopeCreateDialogShellComponent,
    HisHopeDataTableCellDirective,
    HisHopeDataTableComponent,
    HisHopeMaterialFormFieldComponent,
    HisHopeTranslatePipe,
  ],
  template: `<hh-create-dialog-shell
      [title]="title"
      [subtitle]="
        'admin.groupMembersSubtitle'
          | hhTranslate
            : 'Members receive the OpenShip console roles mapped to this group. Removing a member signs them out of OpenShip consoles (their tokens are revoked).'
      "
      ><div hhCreateDialogContent class="members-content">
        @if (data.canWrite) {
          <section
            class="add-member"
            [attr.aria-label]="
              'admin.groupMemberAdd' | hhTranslate: 'Add member'
            "
          >
            <hh-mat-form-field
              [control]="searchControl"
              [label]="'admin.groupMemberSearch' | hhTranslate: 'Find user'"
              [placeholder]="
                'admin.groupMemberSearchPlaceholder'
                  | hhTranslate: 'Search by email or name'
              "
            />
            <hh-mat-form-field
              [control]="userControl"
              kind="select"
              [label]="'admin.user' | hhTranslate: 'User'"
              [options]="userOptions"
            />
            <hh-action-button
              kind="primary"
              icon="person_add"
              [label]="'admin.groupMemberAdd' | hhTranslate: 'Add member'"
              [disabled]="busy || !userControl.value"
              [loading]="busy"
              (pressed)="addMember()"
            />
          </section>
          @if (userSearchError) {
            <p class="error" role="alert">{{ userSearchError }}</p>
          }
        } @else {
          <p class="read-only-hint">
            {{
              "admin.groupMembersReadOnly"
                | hhTranslate
                  : "You can view members but need write access to change them."
            }}
          </p>
        }
        <hh-data-table
          [label]="'admin.groupMembers' | hhTranslate: 'Group members'"
          [columns]="columns"
          [rows]="rows"
          [loading]="loading"
          [error]="loadError"
          [empty]="!loading && !loadError && !rows.length"
          [emptyMessage]="
            'admin.groupMembersEmpty' | hhTranslate: 'This group has no members.'
          "
          ><ng-template hhDataTableCell="actions" let-row>
            <hh-action-button
              kind="danger"
              mode="icon-only"
              icon="person_remove"
              [label]="'admin.groupMemberRemove' | hhTranslate: 'Remove member'"
              [disabled]="busy"
              (pressed)="confirmRemove(row)"
            /> </ng-template
        ></hh-data-table>
      </div>
      <div hhCreateDialogFooter>
        <hh-action-button
          kind="secondary"
          icon="close"
          [label]="'admin.close' | hhTranslate: 'Close'"
          (pressed)="dialogRef.close()"
        /></div
    ></hh-create-dialog-shell>
    <hh-confirm-dialog
      [open]="confirm.open"
      [title]="confirm.title"
      [message]="confirm.message"
      [confirmLabel]="confirm.confirmLabel"
      (confirmed)="confirm.confirm()"
      (cancelled)="confirm.cancel()"
    />`,
  styles: [
    `
      .members-content {
        display: grid;
        gap: var(--space-md);
      }
      .add-member {
        display: grid;
        grid-template-columns: minmax(0, 1fr) minmax(0, 1fr) auto;
        align-items: center;
        gap: var(--space-md);
      }
      @media (max-width: 720px) {
        .add-member {
          grid-template-columns: minmax(0, 1fr);
        }
      }
      .read-only-hint {
        margin: 0;
        color: var(--text-secondary);
      }
      .error {
        margin: 0;
        color: var(--color-danger);
      }
    `,
  ],
})
export class GroupMembersDialogComponent implements OnInit {
  readonly dialogRef = inject(HisHopeDialogRef<GroupMembersDialogComponent>);
  readonly data = inject<GroupMembersDialogData>(HIS_HOPE_DIALOG_DATA);
  private readonly api = inject(IamApiService);
  private readonly usersApi = inject(UsersApiService);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly toast = inject(HisHopeToastService);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);

  readonly confirm = new AdminConfirmState();
  readonly searchControl = new FormControl("", { nonNullable: true });
  readonly userControl = new FormControl("", { nonNullable: true });
  members: IamGroupMember[] = [];
  candidates: User[] = [];
  loading = false;
  loadError = "";
  userSearchError = "";
  busy = false;

  get title(): string {
    return this.i18n.t("admin.groupMembersTitle", "Members of {{group}}", {
      group: this.data.group.displayName,
    });
  }

  get rows(): Record<string, unknown>[] {
    return this.members.map((member) => ({ id: member.userId, ...member }));
  }

  get columns(): HisHopeDataTableColumn[] {
    this.i18n.locale();
    const columns: HisHopeDataTableColumn[] = [
      { key: "email", label: this.i18n.t("admin.email", "Email") },
      {
        key: "displayName",
        label: this.i18n.t("admin.displayName", "Display name"),
        computed: (row) => row["displayName"] || "—",
      },
      {
        key: "createdAt",
        label: this.i18n.t("admin.groupMemberAddedAt", "Added at"),
        format: "dateTime",
      },
      {
        key: "createdBy",
        label: this.i18n.t("admin.groupMemberAddedBy", "Added by"),
        computed: (row) => row["createdBy"] || "—",
      },
    ];
    if (this.data.canWrite) {
      columns.push({
        key: "actions",
        label: this.i18n.t("admin.actions", "Actions"),
        sortable: false,
        hideable: false,
        align: "center",
      });
    }
    return columns;
  }

  get userOptions(): { value: string; label: string }[] {
    const memberIds = new Set(this.members.map((member) => member.userId));
    return [
      { value: "", label: this.i18n.t("admin.select", "Select") },
      ...this.candidates
        .filter((user) => !memberIds.has(user.id))
        .map((user) => ({
          value: user.id,
          label: user.email || user.userName,
        })),
    ];
  }

  ngOnInit(): void {
    this.loadMembers();
    if (this.data.canWrite) {
      this.bindUserSearch();
    }
  }

  loadMembers(): void {
    this.loading = true;
    this.loadError = "";
    this.cdr.markForCheck();
    this.api
      .getGroupMembers(this.data.group.id)
      .pipe(
        finalize(() => {
          this.loading = false;
          this.cdr.markForCheck();
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (members) => {
          this.members = members;
        },
        error: () => {
          this.loadError = this.i18n.t(
            "admin.groupMembersLoadFailed",
            "Unable to load group members.",
          );
        },
      });
  }

  addMember(): void {
    const userId = this.userControl.value;
    if (!this.data.canWrite || !userId || this.busy) return;
    this.busy = true;
    this.cdr.markForCheck();
    this.api
      .addGroupMember(this.data.group.id, userId)
      .pipe(
        finalize(() => {
          this.busy = false;
          this.cdr.markForCheck();
        }),
      )
      .subscribe({
        next: () => {
          this.userControl.setValue("");
          this.toast.success(
            this.i18n.t("admin.groupMemberAdded", "Member added."),
            { duration: 3000 },
          );
          this.loadMembers();
        },
        error: () =>
          this.toast.error(
            this.i18n.t("admin.groupMemberAddFailed", "Unable to add member."),
            { duration: 5000 },
          ),
      });
  }

  confirmRemove(row: Record<string, unknown>): void {
    if (!this.data.canWrite) return;
    const member = this.members.find((item) => item.userId === row["id"]);
    if (!member) return;
    this.confirm.ask(
      this.i18n.t(
        "admin.groupMemberRemoveConfirm",
        "Remove {{user}} from {{group}}? They will be signed out of OpenShip consoles.",
        { user: member.email, group: this.data.group.displayName },
      ),
      () => this.removeMember(member),
      {
        confirmLabel: this.i18n.t("admin.groupMemberRemove", "Remove member"),
      },
    );
    this.cdr.markForCheck();
  }

  private removeMember(member: IamGroupMember): void {
    if (!this.data.canWrite || this.busy) return;
    this.busy = true;
    this.cdr.markForCheck();
    this.api
      .removeGroupMember(this.data.group.id, member.userId)
      .pipe(
        finalize(() => {
          this.busy = false;
          this.cdr.markForCheck();
        }),
      )
      .subscribe({
        next: () => {
          this.toast.success(
            this.i18n.t("admin.groupMemberRemoved", "Member removed."),
            { duration: 3000 },
          );
          this.loadMembers();
        },
        error: () =>
          this.toast.error(
            this.i18n.t(
              "admin.groupMemberRemoveFailed",
              "Unable to remove member.",
            ),
            { duration: 5000 },
          ),
      });
  }

  private bindUserSearch(): void {
    this.searchControl.valueChanges
      .pipe(
        debounceTime(300),
        startWith(this.searchControl.value),
        map((search) => search.trim()),
        distinctUntilChanged(),
        switchMap((search) =>
          this.usersApi
            .getUsersPage({
              page: 1,
              pageSize: USER_SEARCH_PAGE_SIZE,
              ...(search ? { search } : {}),
            })
            .pipe(
              map((page) => ({ users: page.items, failed: false })),
              catchError(() => of({ users: [] as User[], failed: true })),
            ),
        ),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe(({ users, failed }) => {
        this.candidates = users;
        this.userSearchError = failed
          ? this.i18n.t("admin.groupMemberSearchFailed", "Unable to load users.")
          : "";
        this.cdr.markForCheck();
      });
  }
}
