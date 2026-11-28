import { provideZonelessChangeDetection } from "@angular/core";
import { TestBed } from "@angular/core/testing";
import { HttpErrorResponse } from "@angular/common/http";
import { Observable, of, Subject, throwError } from "rxjs";
import { HisHopeI18nService } from "@his-hope/frontend-foundation/i18n";
import { HisHopePermissionService } from "@his-hope/frontend-foundation/auth";
import { HisHopeToastService } from "@his-hope/frontend-foundation/ui";
import {
  AccessGovernanceApiService,
  BreakGlassRequest,
} from "../../core/services/access-governance-api.service";
import {
  InfraAuditApiService,
  InfraAuditEvent,
  InfraAuditGrantTimeline,
} from "../../core/services/infra-audit-api.service";
import { TenantContextService } from "../../core/services/tenant-context.service";
import { BreakGlassPageComponent } from "./break-glass-page.component";

function grant(overrides: Partial<BreakGlassRequest> = {}): BreakGlassRequest {
  return {
    id: "22222222-2222-2222-2222-222222222222",
    subjectUserId: "u1",
    permissionCode: "openship.console.write",
    facilityId: "unassigned",
    reason: "Restore the failed Redis primary",
    status: "approved",
    requestedBy: "u2",
    requestedAt: "2026-11-26T08:00:00Z",
    approvedAt: "2026-11-26T08:05:00Z",
    expiresAt: "2026-11-26T08:35:00Z",
    ...overrides,
  };
}

const event: InfraAuditEvent = {
  timestamp: "2026-11-26T08:10:00Z",
  source: "console",
  actor: "ops@example.com",
  action: "POST redis-rw (write)",
  target: null,
  result: "allowed",
  sourceIp: null,
  correlationId: "r1",
  detail: null,
  logType: "console_audit",
};

function timeline(overrides: Partial<InfraAuditGrantTimeline> = {}): InfraAuditGrantTimeline {
  return {
    grantId: grant().id,
    from: "2026-11-26T08:05:00Z",
    to: "2026-11-26T08:35:00Z",
    items: [event],
    truncated: false,
    ...overrides,
  };
}

function setup(getGrantTimeline: (id: string) => Observable<InfraAuditGrantTimeline>) {
  TestBed.configureTestingModule({
    imports: [BreakGlassPageComponent],
    providers: [
      provideZonelessChangeDetection(),
      { provide: AccessGovernanceApiService, useValue: {} },
      { provide: InfraAuditApiService, useValue: { getGrantTimeline } },
      { provide: TenantContextService, useValue: { bindTenantReload: () => undefined } },
      { provide: HisHopePermissionService, useValue: { snapshot: () => null, has: () => true } },
      { provide: HisHopeToastService, useValue: { success: () => undefined, error: () => undefined } },
      {
        provide: HisHopeI18nService,
        useValue: { t: (_key: string, fallback?: string) => fallback ?? _key, locale: () => "en" },
      },
    ],
  }).overrideComponent(BreakGlassPageComponent, { set: { template: "" } });
  const page = TestBed.createComponent(BreakGlassPageComponent).componentInstance;
  page.requests = [grant(), grant({ id: "33333333-3333-3333-3333-333333333333", status: "pending" })];
  return page;
}

describe("BreakGlassPageComponent activity tab", () => {
  it("opens the Activity tab for the chosen grant and lists its timeline", () => {
    const getGrantTimeline = jasmine.createSpy("getGrantTimeline").and.returnValue(of(timeline()));
    const page = setup(getGrantTimeline);

    page.showActivityByRow({ id: grant().id });

    expect(getGrantTimeline).toHaveBeenCalledOnceWith(grant().id);
    expect(page.selectedTab).toBe(1);
    expect(page.activityGrant?.id).toBe(grant().id);
    expect(page.activityLoading).toBeFalse();
    expect(page.activityRows.length).toBe(1);
    expect(page.activityRows[0]["action"]).toBe("POST redis-rw (write)");
    expect(page.activityRows[0]["target"]).toBe("");
    expect(page.activityColumns.map((column) => column.key)).toContain("timestamp");
  });

  it("refresh reloads the same grant", () => {
    const getGrantTimeline = jasmine.createSpy("getGrantTimeline").and.returnValue(of(timeline()));
    const page = setup(getGrantTimeline);
    page.showActivity(grant());

    page.loadActivity();

    expect(getGrantTimeline).toHaveBeenCalledTimes(2);
    expect(getGrantTimeline.calls.mostRecent().args[0]).toBe(grant().id);
  });

  it("keeps the expired and truncated flags for the tab to explain", () => {
    const page = setup(() => of(timeline({ items: [], expired: true, truncated: true })));

    page.showActivity(grant());

    expect(page.timeline?.expired).toBeTrue();
    expect(page.timeline?.truncated).toBeTrue();
    expect(page.activityRows).toEqual([]);
  });

  it("explains that only super-admins can see infrastructure activity", () => {
    const page = setup(() => throwError(() => new HttpErrorResponse({ status: 403 })));

    page.showActivity(grant());

    expect(page.activityError).toContain("super-admins");
    expect(page.activityLoading).toBeFalse();
  });

  it("reports an unreachable audit store", () => {
    const page = setup(() =>
      throwError(() => new HttpErrorResponse({ status: 503, error: { errorCode: "infra_audit_unavailable" } })),
    );

    page.showActivity(grant());

    expect(page.activityError).toContain("unreachable");
  });

  it("ignores a late answer for a grant that is no longer selected", () => {
    const first = new Subject<InfraAuditGrantTimeline>();
    const responses: Record<string, Observable<InfraAuditGrantTimeline>> = {
      [grant().id]: first,
      "44444444-4444-4444-4444-444444444444": of(timeline({ grantId: "44444444-4444-4444-4444-444444444444", items: [] })),
    };
    const page = setup((id) => responses[id]);

    page.showActivity(grant());
    page.showActivity(grant({ id: "44444444-4444-4444-4444-444444444444" }));
    first.next(timeline());

    expect(page.activityGrant?.id).toBe("44444444-4444-4444-4444-444444444444");
    expect(page.activityRows).toEqual([]);
  });

  it("ignores rows that are not in the current list", () => {
    const getGrantTimeline = jasmine.createSpy("getGrantTimeline");
    const page = setup(getGrantTimeline);

    page.showActivityByRow({ id: "missing" });

    expect(getGrantTimeline).not.toHaveBeenCalled();
    expect(page.selectedTab).toBe(0);
  });
});
