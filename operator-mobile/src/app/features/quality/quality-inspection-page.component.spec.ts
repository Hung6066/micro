import { TestBed } from "@angular/core/testing";
import { QualityInspectionPageComponent } from "./quality-inspection-page.component";
import { OperatorMobileTenantContextService } from "../../core/operator-mobile-tenant-context.service";
import { OperationQueueService } from "../../core/offline/operation-queue.service";
import { OperatorMobileApiService } from "../../core/services/operator-mobile-api.service";

describe("QualityInspectionPageComponent", () => {
  it("does not queue an inspection without a lot and inspector", async () => {
    TestBed.configureTestingModule({ providers: [
      { provide: OperatorMobileTenantContextService, useValue: { commandScope: null } },
      { provide: OperatorMobileApiService, useValue: {} },
      { provide: OperationQueueService, useValue: {} },
    ] });
    const fixture = TestBed.createComponent(QualityInspectionPageComponent);
    const component = fixture.componentInstance;
    component.lotId = "";
    component.inspector = "";
    await component.submitInspection();
    expect(component.message).toContain("mã lô");
  });
});
