import { TestBed } from "@angular/core/testing";
import { MaintenanceWorkPageComponent } from "./maintenance-work-page.component";
import { OperatorMobileTenantContextService } from "../../core/operator-mobile-tenant-context.service";
import { OperationQueueService } from "../../core/offline/operation-queue.service";
import { OperatorMobileApiService } from "../../core/services/operator-mobile-api.service";

describe("MaintenanceWorkPageComponent", () => {
  it("does not complete work until the checklist is complete", async () => {
    TestBed.configureTestingModule({ providers: [
      { provide: OperatorMobileTenantContextService, useValue: { commandScope: null } },
      { provide: OperatorMobileApiService, useValue: {} },
      { provide: OperationQueueService, useValue: {} },
    ] });
    const fixture = TestBed.createComponent(MaintenanceWorkPageComponent);
    const component = fixture.componentInstance;
    component.checklistComplete = false;
    await component.completeWorkOrder();
    expect(component.message).toContain("checklist");
  });
});
