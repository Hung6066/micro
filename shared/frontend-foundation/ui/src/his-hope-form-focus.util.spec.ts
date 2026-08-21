import { focusFirstInvalidControl } from "./his-hope-form-focus.util";

describe("focusFirstInvalidControl", () => {
  it("focuses the first invalid Material field in a host", () => {
    document.body.innerHTML = `
      <form>
        <div class="mat-mdc-form-field hh-mat-field mat-form-field-invalid">
          <input />
        </div>
        <div class="mat-mdc-form-field hh-mat-field mat-form-field-invalid">
          <textarea></textarea>
        </div>
      </form>
    `;
    const host = document.querySelector("form")!;
    const firstInput = host.querySelector("input") as HTMLInputElement;
    spyOn(firstInput, "focus");

    expect(focusFirstInvalidControl(host)).toBeTrue();
    expect(firstInput.focus).toHaveBeenCalled();
  });
});
