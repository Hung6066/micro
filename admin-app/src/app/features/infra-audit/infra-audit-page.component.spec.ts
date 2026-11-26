import { provideZonelessChangeDetection } from "@angular/core";
import { TestBed } from "@angular/core/testing";
import { HttpErrorResponse } from "@angular/common/http";
import { of, throwError } from "rxjs";
import { HisHopeI18nService } from "@his-hope/frontend-foundation/i18n";
import {
  InfraAuditApiService,
  InfraAuditEvent,
  WarpgateOpenSession,
  toParams,
} from "../../core/services/infra-audit-api.service";
import { InfraAuditPageComponent, toRow, toSessionRow } from "./infra-audit-page.component";

const meta = {
  configured: true, sources: ["vault", "warpgate"], results: ["allowed", "denied"],
  retentionDays: 90, maxRangeDays: 31, maxPageSize: 500, maxExportRows: 10000,
};

const openSession: WarpgateOpenSession = {
  sessionId: "6f1c2a3b-0000-4000-8000-00000000000a",
  user: "ops@example.com",
  target: "k3s-manager",
  startedAt: "2026-11-26T09:10:00Z",
  sourceIp: "100.64.0.7",
};

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
      {
        provide: InfraAuditApiService,
        useValue: {
          getWarpgateSessions: () => of({ from: "", to: "", items: [], truncated: false }),
          ...api,
        },
      },
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

describe("InfraAuditPageComponent open Warpgate sessions", () => {
  it("loads open sessions with the page and again on refresh", () => {
    const getWarpgateSessions = jasmine.createSpy("getWarpgateSessions").and.returnValue(
      of({ from: "", to: "", items: [openSession], truncated: true }),
    );
    const page = setup({
      getMeta: () => of(meta),
      getEvents: () => of({ items: [], nextTo: null }),
      getSummary: () => throwError(() => new HttpErrorResponse({ status: 503 })),
      getWarpgateSessions,
    });

    expect(getWarpgateSessions).toHaveBeenCalledOnceWith(24);
    expect(page.sessionRows.length).toBe(1);
    expect(page.sessionRows[0]["user"]).toBe("ops@example.com");
    expect(page.sessionsTruncated).toBeTrue();
    expect(page.sessionColumns.map((column) => column.key)).toEqual([
      "user", "target", "startedAt", "sourceIp", "sessionId",
    ]);

    page.filters.controls.hours.setValue(24 * 7);
    page.loadSessions();
    expect(getWarpgateSessions.calls.mostRecent().args[0]).toBe(168);
  });

  it("never asks for less than a day so long-running sessions stay visible", () => {
    const getWarpgateSessions = jasmine.createSpy("getWarpgateSessions").and.returnValue(
      of({ from: "", to: "", items: [], truncated: false }),
    );
    const page = setup({
      getMeta: () => of(meta),
      getEvents: () => of({ items: [], nextTo: null }),
      getSummary: () => throwError(() => new HttpErrorResponse({ status: 503 })),
      getWarpgateSessions,
    });

    page.filters.controls.hours.setValue(1);
    page.loadSessions();

    expect(getWarpgateSessions.calls.mostRecent().args[0]).toBe(24);
  });

  it("shows the store error in the panel and keeps the rest of the page", () => {
    const page = setup({
      getMeta: () => of(meta),
      getEvents: () => of({ items: [event], nextTo: null }),
      getSummary: () => throwError(() => new HttpErrorResponse({ status: 503 })),
      getWarpgateSessions: () =>
        throwError(() => new HttpErrorResponse({ status: 503, error: { errorCode: "infra_audit_unavailable" } })),
    });

    expect(page.sessionsError).toContain("unreachable");
    expect(page.sessionsLoading).toBeFalse();
    expect(page.rows.length).toBe(1);
  });

  it("does not ask for sessions when only the caller's own activity is allowed", () => {
    const getWarpgateSessions = jasmine.createSpy("getWarpgateSessions");
    setup({
      getMeta: () => throwError(() => new HttpErrorResponse({ status: 403 })),
      getMyEvents: () => of({ items: [], nextTo: null }),
      getWarpgateSessions,
    });

    expect(getWarpgateSessions).not.toHaveBeenCalled();
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

  it("keys session rows by the Warpgate session id and blanks missing fields", () => {
    const row = toSessionRow({ ...openSession, user: null, sourceIp: null });
    expect(row["id"]).toBe(openSession.sessionId);
    expect(row["user"]).toBe("—");
    expect(row["sourceIp"]).toBe("");
  });

  it("gives every row a stable id and dashes for a missing actor", () => {
    const row = toRow({ ...event, actor: null });
    expect(row["actor"]).toBe("—");
    expect(row["id"]).toContain(event.timestamp);
  });
});
