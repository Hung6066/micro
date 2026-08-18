import { Component } from "@angular/core";
import { ComponentFixture, TestBed } from "@angular/core/testing";
import { By } from "@angular/platform-browser";
import { HisHopeActionButtonComponent } from "./his-hope-action-button.component";

@Component({
  standalone: true,
  imports: [HisHopeActionButtonComponent],
  template: `
    <hh-action-button
      kind="primary"
      icon="add"
      label="Create user"
      (pressed)="pressed = true"
    />
    <hh-action-button
      kind="row"
      mode="icon-only"
      icon="delete"
      label="Delete user"
    />
  `,
})
class HostComponent {
  pressed = false;
}

describe("HisHopeActionButtonComponent", () => {
  let fixture: ComponentFixture<HostComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [HostComponent],
    }).compileComponents();
    fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
  });

  it("renders labelled primary actions and icon-only row labels", () => {
    const buttons = fixture.nativeElement.querySelectorAll("button");
    expect(buttons[0].textContent).toContain("Create user");
    expect(buttons[0].getAttribute("data-hh-action")).toBe("primary");
    expect(buttons[1].textContent).not.toContain("Delete user");
    expect(buttons[1].getAttribute("aria-label")).toBe("Delete user");
  });

  it("emits pressed when clicked", () => {
    const host = fixture.componentInstance;
    fixture.debugElement
      .query(By.css("hh-action-button button"))
      .nativeElement.click();
    expect(host.pressed).toBeTrue();
  });
});
