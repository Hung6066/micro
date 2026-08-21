import { AdminConfirmState } from "./admin-confirm-state";

describe("AdminConfirmState", () => {
  it("runs the pending action only after confirm", () => {
    const confirm = new AdminConfirmState();
    const action = jasmine.createSpy("action");

    confirm.ask("admin.confirmRolePublish", action, {
      confirmLabel: "admin.publish",
    });

    expect(confirm.open).toBeTrue();
    expect(confirm.message).toBe("admin.confirmRolePublish");
    expect(action).not.toHaveBeenCalled();

    confirm.confirm();

    expect(action).toHaveBeenCalledTimes(1);
    expect(confirm.open).toBeFalse();
  });

  it("does not run the action when cancelled", () => {
    const confirm = new AdminConfirmState();
    const action = jasmine.createSpy("action");
    confirm.ask("admin.confirmRoleRollback", action);
    confirm.cancel();
    expect(action).not.toHaveBeenCalled();
    expect(confirm.open).toBeFalse();
  });
});
