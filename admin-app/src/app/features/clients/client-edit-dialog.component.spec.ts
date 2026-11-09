import { TestBed } from "@angular/core/testing";
import {
  HIS_HOPE_DIALOG_DATA,
  HisHopeDialogRef,
  HisHopeToastService,
} from "@his-hope/frontend-foundation/ui";
import { HisHopeI18nService } from "@his-hope/frontend-foundation/i18n";
import { ClientsApiService } from "../../core/services/clients-api.service";
import { ClientEditDialogComponent } from "./client-edit-dialog.component";

describe("ClientEditDialogComponent", () => {
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
