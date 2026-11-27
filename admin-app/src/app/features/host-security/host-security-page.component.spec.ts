import { provideZonelessChangeDetection } from "@angular/core";
import { TestBed } from "@angular/core/testing";
import { HttpErrorResponse } from "@angular/common/http";
import { of, throwError } from "rxjs";
import { HisHopeI18nService } from "@his-hope/frontend-foundation/i18n";
import {
  HostIntegrityStatus,
  HostSecurityApiService,
  HostSecurityEvent,
  toParams,
} from "../../core/services/host-security-api.service";
import { HostSecurityPageComponent } from "./host-security-page.component";

const drift: HostSecurityEvent = {
  id: "aaaaaaaaaaaaaaaaaaaa",
  ackKey: "drift:vmi3129107:3dec450c11effb93",
  timestamp: "2026-11-27T08:18:00Z",
  category: "integrity",
  securityEvent: "integrity_drift",
  severity: "critical",
  host: "vmi3129107",
  actor: null,
  summary: "1 unapproved change: added /etc/sudoers.d/zz",
  detail: null,
  fingerprint: "3dec450c11effb93",
  changes: [{ category: "sudoers", path: "/etc/sudoers.d/zz", change: "added", severity: "critical", detail: null }],
  ack: null,
};
const driftRepeat: HostSecurityEvent = { ...drift, id: "bbbbbbbbbbbbbbbbbbbb", timestamp: "2026-11-27T08:13:00Z" };
const sudo: HostSecurityEvent = {
  ...drift, id: "cccccccccccccccccccc", ackKey: "evt:cccccccccccccccccccc", category: "sudo",
  securityEvent: "sudo_failure", severity: "warning", actor: "ops", changes: [], fingerprint: null,
};
const hosts: HostIntegrityStatus[] = [
  { host: "vmi3129107", lastReportAt: "2026-11-27T08:18:00Z", lastEvent: "integrity_drift", severity: "critical", driftCount: 1, fingerprint: "3dec450c11effb93", stale: false },
  { host: "pg-53", lastReportAt: "2026-11-27T07:00:00Z", lastEvent: "integrity_ok", severity: "info", driftCount: 0, fingerprint: null, stale: true },
  { host: "vmi3600038", lastReportAt: "2026-11-27T08:17:00Z", lastEvent: "integrity_ok", severity: "info", driftCount: 0, fingerprint: null, stale: false },
];

function setup(api: Partial<HostSecurityApiService>) {
  TestBed.configureTestingModule({
    imports: [HostSecurityPageComponent],
    providers: [
      provideZonelessChangeDetection(),
      {
        provide: HostSecurityApiService,
        useValue: {
          getMeta: () => of({ configured: true, categories: [], severities: [], maxLimit: 500, maxRangeDays: 31, staleAfterMinutes: 20 }),
          getHosts: () => of({ items: hosts }),
          getEvents: () => of({ items: [drift, driftRepeat, sudo], unacknowledged: { critical: 1, warning: 1 }, truncated: false, unavailableSources: [] }),
          ...api,
        },
      },
      { provide: HisHopeI18nService, useValue: { t: (_key: string, fallback?: string) => fallback ?? _key, locale: () => "en" } },
    ],
  }).overrideComponent(HostSecurityPageComponent, { set: { template: "" } });
  return TestBed.createComponent(HostSecurityPageComponent).componentInstance;
}

describe("HostSecurityPageComponent", () => {
  it("loads events and host status and counts drifted and silent hosts", () => {
    const getEvents = jasmine.createSpy("getEvents").and.callFake(() =>
      of({ items: [drift, sudo], unacknowledged: { critical: 1, warning: 1 }, truncated: false, unavailableSources: [] }));
    const page = setup({ getEvents });
    page.filters.patchValue({ minSeverity: "critical", host: "vmi3129107", unacknowledgedOnly: true });
    page.setLive(false);

    expect(getEvents).toHaveBeenCalledTimes(1);
    const query = getEvents.calls.mostRecent().args[0];
    expect(query.minSeverity).toBe("critical");
    expect(query.host).toBe("vmi3129107");
    expect(query.unacknowledgedOnly).toBeTrue();
    expect(page.page?.items.length).toBe(2);
    expect(page.driftedHosts).toBe(1);
    expect(page.staleHosts).toBe(1);
    expect(page.hostLabel(hosts[0])).toBe("1 drift (critical)");
    expect(page.hostLabel(hosts[1])).toBe("silent");
    expect(page.hostClass(hosts[2])).toBe("host ok");
  });

  it("acknowledges the selected drift once (all its repeats share the key) and reloads", () => {
    const acknowledge = jasmine.createSpy("acknowledge").and.returnValue(of({ acknowledged: 1, items: [] }));
    const getEvents = jasmine.createSpy("getEvents").and.returnValue(
      of({ items: [drift, driftRepeat, sudo], unacknowledged: {}, truncated: false, unavailableSources: [] }));
    const page = setup({ acknowledge, getEvents });
    page.setLive(false);

    page.toggle(drift);
    page.ackNote = "planned sudoers change";
    page.acknowledgeSelected();

    const [events, note] = acknowledge.calls.mostRecent().args;
    expect(events.map((item: HostSecurityEvent) => item.ackKey)).toEqual([drift.ackKey, drift.ackKey]);
    expect(note).toBe("planned sudoers change");
    expect(page.selected.size).toBe(0);
    expect(getEvents).toHaveBeenCalledTimes(2);
  });

  it("shows the server reason when Loki is unavailable", () => {
    const page = setup({
      getEvents: () => throwError(() => new HttpErrorResponse({ status: 503, error: { errorCode: "host_security_unavailable", reason: "upstream_502" } })),
    });
    page.setLive(false);
    expect(page.message).toContain("upstream_502");
    expect(page.loading).toBeFalse();
  });

  it("encodes only the filters that are set", () => {
    const params = toParams({ categories: ["integrity", "runtime"], minSeverity: "warning", host: " pg-53 ", unacknowledgedOnly: false, limit: 300 });
    expect(params.get("categories")).toBe("integrity,runtime");
    expect(params.get("host")).toBe("pg-53");
    expect(params.has("unacknowledgedOnly")).toBeFalse();
    expect(params.has("text")).toBeFalse();
  });
});
