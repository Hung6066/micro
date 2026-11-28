import { provideZonelessChangeDetection } from "@angular/core";
import { TestBed } from "@angular/core/testing";
import { HttpErrorResponse } from "@angular/common/http";
import { of, throwError } from "rxjs";
import { HisHopeI18nService } from "@his-hope/frontend-foundation/i18n";
import { RiskApiService, RiskEvent, RiskEventsPage, RiskSummary, toRiskParams } from "../../core/services/risk-api.service";
import { RISK_PAGE_SIZE, RiskPageComponent } from "./risk-page.component";

const success: RiskEvent = {
  id: "11111111-1111-1111-1111-111111111111",
  timestamp: "2026-11-28T08:00:00Z",
  userId: "22222222-2222-2222-2222-222222222222",
  userName: "admin@example.com",
  eventType: "login_success",
  severity: "info",
  ipPrefix: "203.0.113.0/24",
  score: 35,
  band: "medium",
  reasons: [
    { code: "unfamiliar_device", points: 20 },
    { code: "new_ip_prefix", points: 15 },
  ],
  methods: ["pwd"],
  outcome: null,
};
const wouldStepUp: RiskEvent = { ...success, id: "33333333-3333-3333-3333-333333333333", eventType: "risk_policy_would_step_up", outcome: "report_only" };
const passkey: RiskEvent = { ...success, id: "44444444-4444-4444-4444-444444444444", score: 0, band: "low", reasons: [{ code: "passkey", points: -20 }] };

const summary: RiskSummary = {
  generatedAt: "2026-11-28T08:05:00Z",
  reportOnly: true,
  windows: {
    "24h": { bands: { low: 4, medium: 2, high: 1 }, total: 7, wouldStepUp: 1, wouldDeny: 0 },
    "7d": { bands: { low: 40, medium: 5, high: 2 }, total: 47, wouldStepUp: 3, wouldDeny: 1 },
  },
};

function eventsPage(items: RiskEvent[], totalCount = items.length, page = 1): RiskEventsPage {
  return { items, totalCount, page, pageSize: RISK_PAGE_SIZE };
}

function setup(api: Partial<RiskApiService>) {
  TestBed.configureTestingModule({
    imports: [RiskPageComponent],
    providers: [
      provideZonelessChangeDetection(),
      {
        provide: RiskApiService,
        useValue: {
          getEvents: () => of(eventsPage([success, wouldStepUp, passkey])),
          getSummary: () => of(summary),
          ...api,
        },
      },
      { provide: HisHopeI18nService, useValue: { t: (_key: string, fallback?: string) => fallback ?? _key, locale: () => "en" } },
    ],
  }).overrideComponent(RiskPageComponent, { set: { template: "" } });
  return TestBed.createComponent(RiskPageComponent).componentInstance;
}

describe("RiskPageComponent", () => {
  it("loads the summary and the first page of scored events", () => {
    const getEvents = jasmine.createSpy("getEvents").and.returnValue(of(eventsPage([success, wouldStepUp, passkey])));
    const page = setup({ getEvents });
    page.ngOnInit();

    expect(getEvents).toHaveBeenCalledTimes(1);
    const query = getEvents.calls.mostRecent().args[0];
    expect(query.page).toBe(1);
    expect(query.pageSize).toBe(RISK_PAGE_SIZE);
    expect(query.band).toBe("");
    expect(page.page?.items.length).toBe(3);
    expect(page.summary?.windows["7d"].wouldDeny).toBe(1);
    expect(page.loading).toBeFalse();
  });

  it("filters by band and user and restarts at page 1", () => {
    const getEvents = jasmine.createSpy("getEvents").and.returnValue(of(eventsPage([success], 120, 1)));
    const page = setup({ getEvents });
    page.ngOnInit();
    page.goTo(2);
    expect(getEvents.calls.mostRecent().args[0].page).toBe(2);

    page.filters.patchValue({ band: "high", user: " admin@example.com ", days: 1 });
    const before = Date.now();
    page.search();
    const query = getEvents.calls.mostRecent().args[0];
    expect(query.band).toBe("high");
    expect(query.page).toBe(1);
    const range = new Date(query.to).getTime() - new Date(query.from).getTime();
    expect(range).toBe(86_400_000);
    expect(new Date(query.to).getTime()).toBeGreaterThanOrEqual(before - 1000);
    expect(page.pageCount).toBe(3);
  });

  it("clicking a user filters on that user id", () => {
    const getEvents = jasmine.createSpy("getEvents").and.returnValue(of(eventsPage([success])));
    const page = setup({ getEvents });
    page.filterUser(success);
    expect(page.filters.controls.user.value).toBe(success.userId!);
    expect(getEvents.calls.mostRecent().args[0].user).toBe(success.userId);
  });

  it("labels events, reasons and report-only policy rows", () => {
    const page = setup({});
    expect(page.eventLabel(success)).toBe("sign-in");
    expect(page.eventLabel({ ...success, eventType: "mfa_failed" })).toBe("MFA failed");
    expect(page.eventLabel(wouldStepUp)).toBe("would step up (report-only)");
    expect(page.isWouldAct(wouldStepUp)).toBeTrue();
    expect(page.isWouldAct(success)).toBeFalse();
    expect(page.reasonLabel({ code: "new_ip_prefix", points: 15 })).toBe("new ip prefix +15");
    expect(page.reasonLabel({ code: "passkey", points: -20 })).toBe("passkey -20");
  });

  it("explains a refused read", () => {
    const page = setup({
      getEvents: () => throwError(() => new HttpErrorResponse({ status: 403 })),
    });
    page.ngOnInit();
    expect(page.message).toBe("Super-admin only.");
    expect(page.loading).toBeFalse();
  });

  it("encodes only the filters that are set", () => {
    const params = toRiskParams({ band: "", user: "  ", page: 2, pageSize: 50 });
    expect(params.has("band")).toBeFalse();
    expect(params.has("user")).toBeFalse();
    expect(params.get("page")).toBe("2");
    expect(toRiskParams({ band: "medium", user: " ops " }).get("user")).toBe("ops");
  });
});
