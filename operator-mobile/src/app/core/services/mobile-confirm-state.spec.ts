import { HisHopeMobileConfirmState } from "@his-hope/mobile-foundation/angular";

describe("MobileConfirmState", () => {
  it("runs the pending action only after confirm", () => {
    const confirm = new HisHopeMobileConfirmState();
    const action = jasmine.createSpy("action");

    confirm.ask("common.confirmContinue", action, {
      title: "common.confirmAction",
      confirmLabel: "common.yes",
    });

    expect(confirm.open).toBeTrue();
    expect(action).not.toHaveBeenCalled();
    confirm.confirm();
    expect(action).toHaveBeenCalledTimes(1);
    expect(confirm.open).toBeFalse();
  });
});
