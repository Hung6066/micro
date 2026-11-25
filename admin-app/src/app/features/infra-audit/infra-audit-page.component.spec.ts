import { provideZonelessChangeDetection } from "@angular/core";
import { TestBed } from "@angular/core/testing";
import { HttpErrorResponse } from "@angular/common/http";
import { of, throwError } from "rxjs";
import { HisHopeI18nService } from "@his-hope/frontend-foundation/i18n";
import {
  InfraAuditApiService,
  InfraAuditEvent,
  toParams,
} from "../../core/services/infra-audit-api.service";
import { InfraAuditPageComponent, toRow } from "./infra-audit-page.component";

const event: InfraAuditEvent = {
  timestamp: "2026-11-25T01:00:00Z",
  source: "vault",
  actor: "ops@example.com",
  action: "update ssh-client-signer/sign/ops",
  target: "ssh-client-signer/sign/ops",
  result: "allowed",
  sourceIp: null,
  correlationId: null,
  detail: null,
  logType: "vault_audit",
};

function setup(api: Partial<InfraAuditApiService>) {
  TestBed.configureTestingModule({
    imports: [InfraAuditPageComponent],
    providers: [
      provideZonelessChangeDetection(),
      { provide: InfraAuditApiService, useValue: api },
      {
        provide: HisHopeI18nService,
        useValue: { t: (_key: string, fallback?: string) => fallback ?? _key, locale: () => "en" },
      },
    ],
  }).overrideComponent(InfraAuditPageComponent, { set: { template: "" } });
  const fixture = TestBed.createComponent(InfraAuditPageComponent);
  fixture.componentInstance.ngOnInit();
  return fixture.componentInstance;
}

describe("InfraAuditPageComponent", () => {
  it("super-admins load events and the summary for the chosen sources", () => {
    const getEvents = jasmine.createSpy("getEvents").and.returnValue(of({ items: [event], nextTo: "2026-11-25T00:59:59Z" }));
    const getSummary = jasmine.createSpy("getSummary").and.returnValue(of({
      from: "", to: "", total: 1, truncated: false, bySource: { vault: 1 }, byResult: { allowed: 1 },
      topActors: [], topTargets: [], perHour: [],
    }));
    const page = setup({
      getMeta: () => of({
        configured: true, sources: ["vault", "console"], results: ["allowed", "denied"],
        retentionDays: 90, maxRangeDays: 31, maxPageSize: 500, maxExportRows: 10000,
      }),
      getEvents,
      getSummary,
    });

    expect(page.mineOnly).toBeFalse();
    expect(page.sources).toEqual(["vault", "console"]);
    expect(getEvents).toHaveBeenCalledTimes(1);
    expect(getSummary).toHaveBeenCalledTimes(1);
    expect(page.rows.length).toBe(1);
    expect(page.nextTo).toBe("2026-11-25T00:59:59Z");

    page.loadOlder();
    expect(getEvents.calls.mostRecent().args[0].to).toBe("2026-11-25T00:59:59Z");
    expect(page.rows.length).toBe(2);
  });

  it("falls back to the caller's own activity when the full audit is forbidden", () => {
    const getMyEvents = jasmine.createSpy("getMyEvents").and.returnValue(of({ items: [event], nextTo: null }));
    const getEvents = jasmine.createSpy("getEvents");
    const page = setup({
      getMeta: () => throwError(() => new HttpErrorResponse({ status: 403 })),
      getMyEvents,
      getEvents,
    });

    expect(page.mineOnly).toBeTrue();
    expect(getMyEvents).toHaveBeenCalled();
    expect(getEvents).not.toHaveBeenCalled();
    expect(getMyEvents.calls.mostRecent().args[0].actor).toBeUndefined();
  });

  it("explains that export needs a fresh MFA", () => {
    const page = setup({
      getMeta: () => of({
        configured: true, sources: [], results: [], retentionDays: 90, maxRangeDays: 31, maxPageSize: 500, maxExportRows: 10000,
      }),
      getEvents: () => of({ items: [], nextTo: null }),
      getSummary: () => throwError(() => new HttpErrorResponse({ status: 503 })),
      export: () => throwError(() => new HttpErrorResponse({ status: 403, error: { errorCode: "step_up_required" } })),
    });

    page.exportCsv();

    expect(page.message).toContain("fresh MFA");
    expect(page.exporting).toBeFalse();
  });

  it("shows nothing and warns when the audit source is not configured", () => {
    const getEvents = jasmine.createSpy("getEvents");
    const page = setup({
      getMeta: () => of({
        configured: false, sources: [], results: [], retentionDays: 90, maxRangeDays: 31, maxPageSize: 500, maxExportRows: 10000,
      }),
      getEvents,
    });

    expect(page.configured).toBeFalse();
    expect(getEvents).not.toHaveBeenCalled();
  });
});

describe("infra audit helpers", () => {
  it("builds query params without empty filters", () => {
    const params = toParams({ from: "a", to: "b", sources: ["vault", "console"], actor: "  ", text: "x", limit: 50 });
    expect(params.get("sources")).toBe("vault,console");
    expect(params.has("actor")).toBeFalse();
    expect(params.get("text")).toBe("x");
    expect(params.get("limit")).toBe("50");
  });

  it("gives every row a stable id and dashes for a missing actor", () => {
    const row = toRow({ ...event, actor: null });
    expect(row["actor"]).toBe("—");
    expect(row["id"]).toContain(event.timestamp);
  });
});
