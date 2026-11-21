import { TestBed } from "@angular/core/testing";
import { SyncPageComponent } from "./sync-page.component";
import { OperationQueueService } from "../../core/offline/operation-queue.service";
import type { QueuedOperation } from "../../core/offline/operation-queue.models";
import { OperatorMobileApiService } from "../../core/services/operator-mobile-api.service";

describe("SyncPageComponent", () => {
  it("requires explicit confirmation before discarding a queued record", async () => {
    const discard = jasmine.createSpy("discard").and.resolveTo();
    const refresh = jasmine.createSpy("entries").and.resolveTo([]);
    TestBed.configureTestingModule({
      providers: [
        {
          provide: OperationQueueService,
          useValue: { discard, entries: refresh },
        },
        { provide: OperatorMobileApiService, useValue: {} },
      ],
    });
    const fixture = TestBed.createComponent(SyncPageComponent);
    const component = fixture.componentInstance;
    const entry = { id: "queued-1" } as QueuedOperation;

    component.requestDiscard(entry);
    expect(discard).not.toHaveBeenCalled();

    await component.confirmDiscard(entry);
    expect(discard).toHaveBeenCalledOnceWith("queued-1");
    expect(component.pendingDiscardId).toBeNull();
  });
});
