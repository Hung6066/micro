import { TestBed } from "@angular/core/testing";
import {
  HIS_HOPE_DIALOG_DATA,
  HisHopeDialogRef,
  HisHopeToastService,
} from "@his-hope/frontend-foundation/ui";
import { of } from "rxjs";
import { IamGroup, User } from "../../core/contracts/admin.contracts";
import { IamGroupMember } from "../../core/contracts/iam.contracts";
import { IamApiService } from "../../core/services/iam-api.service";
import { UsersApiService } from "../../core/services/users-api.service";
import {
  GroupMembersDialogComponent,
  GroupMembersDialogData,
} from "./group-members-dialog.component";

describe("GroupMembersDialogComponent", () => {
  const group: IamGroup = {
    id: "group-1",
    key: "openship-console-viewer",
    displayName: "OpenShip console viewers",
    scopeId: "scope-1",
    isActive: true,
  };
  const member: IamGroupMember = {
    userId: "user-1",
    email: "viewer@example.com",
    displayName: "Console Viewer",
    createdAt: "2026-11-20T10:00:00Z",
    createdBy: "admin@example.com",
  };
  const candidate: User = {
    id: "user-2",
    userName: "netadmin",
    email: "netadmin@example.com",
    roles: [],
    isActive: true,
  };

  let api: jasmine.SpyObj<IamApiService>;
  let toast: { success: jasmine.Spy; error: jasmine.Spy };

  function configure(canWrite: boolean): void {
    api = jasmine.createSpyObj<IamApiService>("IamApiService", [
      "getGroupMembers",
      "addGroupMember",
      "removeGroupMember",
    ]);
    api.getGroupMembers.and.returnValue(of([member]));
    api.addGroupMember.and.returnValue(of(undefined));
    api.removeGroupMember.and.returnValue(of(undefined));
    const usersApi = jasmine.createSpyObj<UsersApiService>("UsersApiService", [
      "getUsersPage",
    ]);
    usersApi.getUsersPage.and.returnValue(
      of({
        items: [
          candidate,
          { ...candidate, id: member.userId, email: member.email },
        ],
        totalCount: 2,
        page: 1,
        pageSize: 20,
        totalPages: 1,
        hasNextPage: false,
        hasPreviousPage: false,
      }),
    );
    toast = {
      success: jasmine.createSpy("success"),
      error: jasmine.createSpy("error"),
    };
    const data: GroupMembersDialogData = { group, canWrite };
    TestBed.configureTestingModule({
      imports: [GroupMembersDialogComponent],
      providers: [
        { provide: IamApiService, useValue: api },
        { provide: UsersApiService, useValue: usersApi },
        { provide: HIS_HOPE_DIALOG_DATA, useValue: data },
        {
          provide: HisHopeDialogRef,
          useValue: { close: jasmine.createSpy("close") },
        },
        { provide: HisHopeToastService, useValue: toast },
      ],
    });
  }

  it("renders the group members and offers only non-members to add", () => {
    configure(true);
    const fixture = TestBed.createComponent(GroupMembersDialogComponent);
    fixture.detectChanges();

    expect(api.getGroupMembers).toHaveBeenCalledWith("group-1");
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain("viewer@example.com");
    expect(text).toContain("Console Viewer");
    expect(text).toContain("OpenShip consoles");
    expect(
      fixture.componentInstance.userOptions.map((option) => option.value),
    ).toEqual(["", "user-2"]);
  });

  it("adds the selected user and reloads the members", () => {
    configure(true);
    const fixture = TestBed.createComponent(GroupMembersDialogComponent);
    fixture.detectChanges();
    const component = fixture.componentInstance;

    component.userControl.setValue("user-2");
    component.addMember();

    expect(api.addGroupMember).toHaveBeenCalledWith("group-1", "user-2");
    expect(api.getGroupMembers).toHaveBeenCalledTimes(2);
    expect(component.userControl.value).toBe("");
    expect(toast.success).toHaveBeenCalled();
  });

  it("removes a member only after confirmation", () => {
    configure(true);
    const fixture = TestBed.createComponent(GroupMembersDialogComponent);
    fixture.detectChanges();
    const component = fixture.componentInstance;

    component.confirmRemove({ id: "user-1" });
    expect(component.confirm.open).toBeTrue();
    expect(api.removeGroupMember).not.toHaveBeenCalled();

    component.confirm.confirm();

    expect(api.removeGroupMember).toHaveBeenCalledWith("group-1", "user-1");
    expect(api.getGroupMembers).toHaveBeenCalledTimes(2);
    expect(toast.success).toHaveBeenCalled();
  });

  it("is read-only without write permission", () => {
    configure(false);
    const fixture = TestBed.createComponent(GroupMembersDialogComponent);
    fixture.detectChanges();
    const component = fixture.componentInstance;

    expect(fixture.nativeElement.textContent).toContain("viewer@example.com");
    expect(component.columns.some((column) => column.key === "actions")).toBe(
      false,
    );

    component.userControl.setValue("user-2");
    component.addMember();
    component.confirmRemove({ id: "user-1" });

    expect(api.addGroupMember).not.toHaveBeenCalled();
    expect(component.confirm.open).toBeFalse();
  });
});
