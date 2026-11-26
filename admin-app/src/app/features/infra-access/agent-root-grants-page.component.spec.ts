import { provideZonelessChangeDetection } from "@angular/core";
import { TestBed } from "@angular/core/testing";
import { HttpErrorResponse } from "@angular/common/http";
import { of, throwError } from "rxjs";
import { HisHopeI18nService } from "@his-hope/frontend-foundation/i18n";
import {
  AgentRootGrant,
  AgentRootGrantList,
  AgentRootGrantsApiService,
} from "../../core/services/agent-root-grants-api.service";
import { AgentRootGrantsPageComponent, toRow } from "./agent-root-grants-page.component";

function grant(overrides: Partial<AgentRootGrant> = {}): AgentRootGrant {
  return {
    id: "11111111-1111-1111-1111-111111111111",
    agentId: "openship-agent-root",
    reason: "Patch kubelet on all nodes",
    durationMinutes: 60,
    status: "pending",
    requestedBy: "a",
    requestedByEmail: "a@example.test",
    requestedAt: "2026-11-26T01:00:00Z",
    approvedBy: null,
    approvedByEmail: null,
    approvedAt: null,
    validBefore: null,
    certificateSerial: null,
    revokedBy: null,
    revokedAt: null,
    failureReason: null,
    active: false,
    remainingMinutes: 0,
    requestedByMe: false,
    pendingUntil: "2026-11-26T02:00:00Z",
    ...overrides,
  };
}

function listOf(items: AgentRootGrant[], enabled = true): AgentRootGrantList {
  return { enabled, agentId: "openship-agent-root", principal: "root-breakglass", minMinutes: 30, maxMinutes: 480, pendingTtlMinutes: 60, items };
}

function setup(api: Partial<AgentRootGrantsApiService>) {
  TestBed.configureTestingModule({
    imports: [AgentRootGrantsPageComponent],
    providers: [
      provideZonelessChangeDetection(),
      { provide: AgentRootGrantsApiService, useValue: api },
      {
        provide: HisHopeI18nService,
        useValue: { t: (_key: string, fallback?: string) => fallback ?? _key, locale: () => "en" },
      },
    ],
  }).overrideComponent(AgentRootGrantsPageComponent, { set: { template: "" } });
  const fixture = TestBed.createComponent(AgentRootGrantsPageComponent);
  fixture.componentInstance.ngOnInit();
  return fixture.componentInstance;
}

describe("AgentRootGrantsPageComponent", () => {
  it("renders the grant list without any certificate field", () => {
    const issued = grant({
      id: "22222222-2222-2222-2222-222222222222",
      status: "issued",
      approvedByEmail: "b@example.test",
      validBefore: "2026-11-26T03:00:00Z",
      active: true,
      remainingMinutes: 95,
    });
    const page = setup({ list: () => of(listOf([grant(), issued])) });

    expect(page.rows.length).toBe(2);
    expect(page.rows.some((row) => "certificate" in row)).toBeFalse();
    const remaining = page.columns.find((column) => column.key === "remainingMinutes")!;
    expect(remaining.computed!(page.rows[1])).toBe("1 h 35 min");
    expect(page.columns.map((column) => column.key)).toContain("actions");
  });

  it("submits the form with the chosen duration and trimmed reason, then reloads", () => {
    const list = jasmine.createSpy("list").and.returnValue(of(listOf([])));
    const create = jasmine.createSpy("create").and.returnValue(of(grant({ requestedByMe: true })));
    const page = setup({ list, create });

    page.form.setValue({ durationMinutes: 240, reason: "  short " });
    expect(page.canSubmit).toBeFalse();
    page.submit();
    expect(create).not.toHaveBeenCalled();

    page.form.setValue({ durationMinutes: 240, reason: "  Rotate the etcd encryption key  " });
    expect(page.canSubmit).toBeTrue();
    page.submit();

    expect(create).toHaveBeenCalledOnceWith({ durationMinutes: 240, reason: "Rotate the etcd encryption key" });
    expect(list).toHaveBeenCalledTimes(2);
    expect(page.form.controls.reason.value).toBe("");
  });

  it("hides Approve for the current user's own request but still allows Revoke", () => {
    const own = grant({ requestedByMe: true });
    const other = grant({ id: "33333333-3333-3333-3333-333333333333" });
    const page = setup({ list: () => of(listOf([own, other])) });

    expect(page.canApprove(toRow(own))).toBeFalse();
    expect(page.canRevoke(toRow(own))).toBeTrue();
    expect(page.canApprove(toRow(other))).toBeTrue();
    expect(page.canApprove(toRow(grant({ status: "issued" })))).toBeFalse();
    expect(page.canRevoke(toRow(grant({ status: "revoked" })))).toBeFalse();
  });

  it("explains step-up and four-eyes errors from the API", () => {
    const approve = jasmine.createSpy("approve").and.returnValue(
      throwError(() => new HttpErrorResponse({ status: 403, error: { errorCode: "agent_root_self_approval" } })),
    );
    const create = jasmine.createSpy("create").and.returnValue(
      throwError(() => new HttpErrorResponse({ status: 403, error: { errorCode: "step_up_required" } })),
    );
    const page = setup({ list: () => of(listOf([grant()])), approve, create });

    page.approve(page.rows[0]);
    expect(page.message).toContain("another super admin");
    expect(page.busy).toBeFalse();

    page.form.setValue({ durationMinutes: 60, reason: "Patch kubelet on all nodes" });
    page.submit();
    expect(page.message).toContain("fresh MFA");
  });

  it("disables requests when the feature is off on the server", () => {
    const page = setup({ list: () => of(listOf([], false)) });
    page.form.setValue({ durationMinutes: 60, reason: "Patch kubelet on all nodes" });
    expect(page.enabled).toBeFalse();
    expect(page.canSubmit).toBeFalse();
  });
});
