import { ChangeDetectorRef } from "@angular/core";
import { TestBed } from "@angular/core/testing";
import { of } from "rxjs";
import { MobileOperationsApiService } from "../../core/services/mobile-operations-api.service";
import { MobileOperationsPageComponent } from "./mobile-operations-page.component";

describe("MobileOperationsPageComponent", () => {
  it("marks an OnPush view for rendering when both delivery requests finish", () => {
    const cdr = jasmine.createSpyObj<ChangeDetectorRef>("ChangeDetectorRef", [
      "markForCheck",
    ]);
    const api = jasmine.createSpyObj<MobileOperationsApiService>(
      "MobileOperationsApiService",
      ["getMobileDevices", "getMobileDeliverySummary"],
    );
    api.getMobileDevices.and.returnValue(
      of({ items: [], page: 1, pageSize: 50, total: 0 }),
    );
    api.getMobileDeliverySummary.and.returnValue(
      of({
        since: new Date().toISOString(),
        queued: 0,
        processed: 0,
        pending: 0,
        sent: 0,
        failed: 0,
        byPlatform: [],
      }),
    );

    TestBed.configureTestingModule({
      providers: [
        { provide: MobileOperationsApiService, useValue: api },
        { provide: ChangeDetectorRef, useValue: cdr },
      ],
    });
    const component = TestBed.runInInjectionContext(
      () => new MobileOperationsPageComponent(),
    );

    component.load();

    expect(component.loading).toBeFalse();
    expect(component.devices).toEqual([]);
    expect(cdr.markForCheck).toHaveBeenCalled();
  });
});
