import { TestBed } from "@angular/core/testing";
import { ActivatedRoute, convertToParamMap } from "@angular/router";
import { MobileNativeMfaComponent } from "./mobile-native-mfa.component";
import { NativeCapabilityService } from "./core/native-capability.service";

describe("MobileNativeMfaComponent", () => {
  async function runWith(
    result: { approved: boolean; status: "approved" | "rejected" | "expired" | "cancelled" | "unsupported" },
    ticket = "ticket-123",
  ): Promise<MobileNativeMfaComponent> {
    TestBed.configureTestingModule({
      providers: [
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { queryParamMap: convertToParamMap(ticket ? { ticket } : {}) } },
        },
        {
          provide: NativeCapabilityService,
          useValue: {
            approveMfa: jasmine.createSpy("approveMfa").and.resolveTo(result),
          },
        },
      ],
    });
    const component = TestBed.runInInjectionContext(() => new MobileNativeMfaComponent());
    await component.ngOnInit();
    return component;
  }

  it("shows approved after the shared native bridge approves the ticket", async () => {
    const component = await runWith({ approved: true, status: "approved" });

    expect(component.status()).toBe("approved");
  });

  it("shows rejected when the server rejects the assertion", async () => {
    const component = await runWith({ approved: false, status: "rejected" });

    expect(component.status()).toBe("rejected");
  });

  it("shows expired when the approval ticket is no longer valid", async () => {
    const component = await runWith({ approved: false, status: "expired" });

    expect(component.status()).toBe("expired");
  });

  it("shows retry when the native passkey prompt is cancelled", async () => {
    const component = await runWith({ approved: false, status: "cancelled" });

    expect(component.status()).toBe("retry");
  });

  it("shows retry when the device cannot perform native approval", async () => {
    const component = await runWith({ approved: false, status: "unsupported" });

    expect(component.status()).toBe("retry");
  });
});
