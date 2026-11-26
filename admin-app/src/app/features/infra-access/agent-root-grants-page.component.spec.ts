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
    requestedDevices: [],
    certificates: [],
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
  return { enabled, agentId: "openship-agent-root", principal: "root-breakglass", minMinutes: 30, maxMinutes: 480, pendingTtlMinutes: 60, activeDevices: [{ id: "dev-k3s", name: "k3s-manager-vmi3129107" }, { id: "dev-win", name: "win-liam" }], items };
}

function setup(api: Partial<AgentRootGrantsApiService>) {
  TestBed.configureTestingModule({
    imports: [AgentRootGrantsPageComponent],
    providers: [
      provideZonelessChangeDetection(),
      { provide: AgentRootGrantsApiService, useValue: { listDevices: () => of([]), ...api } },
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
    expect(page.activeDevices.map((device) => device.name)).toEqual(["k3s-manager-vmi3129107", "win-liam"]);
    // The K3s manager is preselected.
    expect(page.form.controls.deviceIds.value).toEqual(["dev-k3s"]);
  });

  it("shows which devices got a certificate for a grant", () => {
    const row = toRow(grant({ status: "issued", certificates: [
      { deviceName: "k3s-manager", serial: "1" },
      { deviceName: "mac-hung", serial: "2" },
    ] }));
    expect(row["devices"]).toBe("k3s-manager, mac-hung");
    expect(toRow(grant({ requestedDevices: ["win-liam", "mac-hung"] }))["devices"]).toBe("win-liam, mac-hung");
  });

  it("adds a device with trimmed fields and toggles it", () => {
    const device = {
      id: "d1", name: "mac-hung", fingerprint: "SHA256:x", clientId: "agent-mac", isActive: true,
      addedByEmail: "a@example.test", addedAt: "2026-11-26T01:00:00Z", disabledAt: null, lastFetchedAt: null,
    };
    const addDevice = jasmine.createSpy("addDevice").and.returnValue(of(device));
    const setDeviceActive = jasmine.createSpy("setDeviceActive").and.returnValue(of({ ...device, isActive: false }));
    const page = setup({ list: () => of(listOf([])), listDevices: () => of([device]), addDevice, setDeviceActive });

    expect(page.deviceRows.length).toBe(1);
    page.deviceForm.setValue({ name: "bad name", clientId: "agent-mac", publicKey: "ssh-ed25519 AAAA x" });
    expect(page.canAddDevice).toBeFalse();
    page.deviceForm.setValue({ name: " mac-hung ", clientId: " agent-mac ", publicKey: " ssh-ed25519 AAAA mac " });
    expect(page.canAddDevice).toBeTrue();
    page.addDevice();
    expect(addDevice).toHaveBeenCalledOnceWith({ name: "mac-hung", clientId: "agent-mac", publicKey: "ssh-ed25519 AAAA mac" });

    page.toggleDevice(page.deviceRows[0]);
    expect(setDeviceActive).toHaveBeenCalledOnceWith("d1", false);
  });

  it("explains a client already used by another device", () => {
    const addDevice = jasmine.createSpy("addDevice").and.returnValue(
      throwError(() => new HttpErrorResponse({ status: 409, error: { errorCode: "agent_root_client_in_use" } })),
    );
    const page = setup({ list: () => of(listOf([])), addDevice });
    page.deviceForm.setValue({ name: "win-hung", clientId: "agent-win", publicKey: "ssh-ed25519 AAAA win" });
    page.addDevice();
    expect(page.message).toContain("one client per device");
  });

  it("submits the form with the chosen duration and trimmed reason, then reloads", () => {
    const list = jasmine.createSpy("list").and.returnValue(of(listOf([])));
    const create = jasmine.createSpy("create").and.returnValue(of(grant({ requestedByMe: true })));
    const page = setup({ list, create });

    page.form.setValue({ durationMinutes: 240, deviceIds: ["dev-k3s"], reason: "  short " });
    expect(page.canSubmit).toBeFalse();
    page.submit();
    expect(create).not.toHaveBeenCalled();

    page.form.setValue({ durationMinutes: 1440, deviceIds: [], reason: "  Rotate the etcd encryption key  " });
    expect(page.canSubmit).toBeFalse();
    page.form.setValue({ durationMinutes: 1440, deviceIds: ["dev-k3s", "dev-win"], reason: "  Rotate the etcd encryption key  " });
    expect(page.canSubmit).toBeTrue();
    page.submit();

    expect(create).toHaveBeenCalledOnceWith({
      durationMinutes: 1440,
      reason: "Rotate the etcd encryption key",
      deviceIds: ["dev-k3s", "dev-win"],
    });
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

    page.form.setValue({ durationMinutes: 60, deviceIds: ["dev-k3s"], reason: "Patch kubelet on all nodes" });
    page.submit();
    expect(page.message).toContain("fresh MFA");
  });

  it("disables requests when the feature is off on the server", () => {
    const page = setup({ list: () => of(listOf([], false)) });
    page.form.setValue({ durationMinutes: 60, deviceIds: ["dev-k3s"], reason: "Patch kubelet on all nodes" });
    expect(page.enabled).toBeFalse();
    expect(page.canSubmit).toBeFalse();
  });
});
