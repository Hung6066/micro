import { provideZonelessChangeDetection } from "@angular/core";
import { TestBed } from "@angular/core/testing";
import { Subject } from "rxjs";
import { ClientSecretResponse } from "../../core/contracts/admin.contracts";
import {
  HIS_HOPE_DIALOG_DATA,
  HisHopeDialogRef,
  HisHopeToastService,
} from "@his-hope/frontend-foundation/ui";
import { HisHopeI18nService } from "@his-hope/frontend-foundation/i18n";
import { ClientsApiService } from "../../core/services/clients-api.service";
import { ClientEditDialogComponent } from "./client-edit-dialog.component";

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
          useValue: { createClient: () => created },
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

  it("offers the OpenShip client reconciliation scope to confidential clients", () => {
    TestBed.configureTestingModule({
      imports: [ClientEditDialogComponent],
      providers: [
        { provide: HIS_HOPE_DIALOG_DATA, useValue: null },
        { provide: ClientsApiService, useValue: {} },
        { provide: HisHopeDialogRef, useValue: {} },
        { provide: HisHopeToastService, useValue: {} },
        { provide: HisHopeI18nService, useValue: { t: (key: string) => key } },
      ],
    });

    const fixture = TestBed.createComponent(ClientEditDialogComponent);
    const component = fixture.componentInstance as unknown as {
      scopeOptions: ReadonlyArray<{ value: string; label: string }>;
    };

    expect(component.scopeOptions).toContain(
      jasmine.objectContaining({
        value: "identity.clients.reconcile",
        label: "identity.clients.reconcile",
      }),
    );
  });
});
