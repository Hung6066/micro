import { provideZonelessChangeDetection } from "@angular/core";
import { TestBed } from "@angular/core/testing";
import { Subject, of, throwError } from "rxjs";
import { ClientSecretResponse } from "../../core/contracts/admin.contracts";
import {
  HIS_HOPE_DIALOG_DATA,
  HisHopeDialogRef,
  HisHopeToastService,
} from "@his-hope/frontend-foundation/ui";
import { HisHopeI18nService } from "@his-hope/frontend-foundation/i18n";
import { ClientsApiService } from "../../core/services/clients-api.service";
import {
  ClientEditDialogComponent,
  isAllowedRedirectUri,
} from "./client-edit-dialog.component";

describe("isAllowedRedirectUri", () => {
  it("accepts HTTPS and HTTP loopback callbacks used by CLI clients", () => {
    expect(isAllowedRedirectUri("https://console.example/callback")).toBeTrue();
    expect(isAllowedRedirectUri("http://localhost:8000")).toBeTrue();
    expect(isAllowedRedirectUri("http://localhost:18000")).toBeTrue();
    expect(isAllowedRedirectUri("http://127.0.0.1:8250/oidc/callback")).toBeTrue();
    expect(isAllowedRedirectUri("http://[::1]:8000/")).toBeTrue();
  });

  it("rejects clear-text non-loopback, fragments, credentials and junk", () => {
    expect(isAllowedRedirectUri("http://console.example/callback")).toBeFalse();
    expect(isAllowedRedirectUri("http://localhost.example.com/cb")).toBeFalse();
    expect(isAllowedRedirectUri("http://127.0.0.2/cb")).toBeFalse();
    expect(isAllowedRedirectUri("https://console.example/cb#frag")).toBeFalse();
    expect(isAllowedRedirectUri("https://user:pass@console.example/cb")).toBeFalse();
    expect(isAllowedRedirectUri("javascript:alert(1)")).toBeFalse();
    expect(isAllowedRedirectUri("not a url")).toBeFalse();
  });
});

describe("ClientEditDialogComponent", () => {
  it("re-enables Save after an asynchronous client creation failure", async () => {
    const created = new Subject<ClientSecretResponse>();
    TestBed.configureTestingModule({
      imports: [ClientEditDialogComponent],
      providers: [
        provideZonelessChangeDetection(),
        { provide: HIS_HOPE_DIALOG_DATA, useValue: null },
        {
          provide: ClientsApiService,
          useValue: { createClient: () => created, getAvailableScopes: () => of([]) },
        },
        { provide: HisHopeDialogRef, useValue: {} },
        { provide: HisHopeToastService, useValue: { error: () => {} } },
        { provide: HisHopeI18nService, useValue: { t: (key: string) => key } },
      ],
    }).overrideComponent(ClientEditDialogComponent, {
      set: {
        template: '<button [disabled]="saving" (click)="save()">Save</button>',
      },
    });
    const fixture = TestBed.createComponent(ClientEditDialogComponent);
    fixture.componentInstance.formGroup.patchValue({
      clientId: "test-console",
      displayName: "Test console",
      redirectUris: "https://console.example/callback",
    });
    await fixture.whenStable();
    const button: HTMLButtonElement = fixture.nativeElement.querySelector("button");
    button.click();
    await fixture.whenStable();
    expect(button.disabled).toBeTrue();

    created.error(new Error("Client creation denied"));
    await fixture.whenStable();

    expect(button.disabled).toBeFalse();
  });

  function createWithScopes(
    getAvailableScopes: () => unknown,
    data: unknown = null,
  ) {
    TestBed.configureTestingModule({
      imports: [ClientEditDialogComponent],
      providers: [
        { provide: HIS_HOPE_DIALOG_DATA, useValue: data },
        { provide: ClientsApiService, useValue: { getAvailableScopes } },
        { provide: HisHopeDialogRef, useValue: {} },
        { provide: HisHopeToastService, useValue: {} },
        { provide: HisHopeI18nService, useValue: { t: (key: string) => key } },
      ],
    });
    return TestBed.createComponent(ClientEditDialogComponent).componentInstance;
  }

  it("offers the scopes IdentityService reports, without a hardcoded list", () => {
    const component = createWithScopes(() =>
      of([
        { name: "openid" },
        { name: "identity.clients.reconcile" },
        { name: "openship.agent.root.certificate" },
      ]),
    );

    expect(component.scopeOptions.map((option) => option.value)).toEqual([
      "identity.clients.reconcile",
      "openid",
      "openship.agent.root.certificate",
    ]);
  });

  it("falls back to the standard OIDC scopes plus the client's own when the list fails", () => {
    const component = createWithScopes(
      () => throwError(() => new Error("offline")),
      { clientId: "legacy", scopes: ["openid", "legacy.scope"] },
    );
    const values = component.scopeOptions.map((option) => option.value);

    expect(values).toContain("legacy.scope");
    expect(values).toContain("offline_access");
    expect(values).not.toContain("openship.console.access");
  });
});
