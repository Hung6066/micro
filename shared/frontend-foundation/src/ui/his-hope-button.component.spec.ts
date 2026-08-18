import { Component } from "@angular/core";
import { ComponentFixture, TestBed } from "@angular/core/testing";
import { HisHopeButtonComponent } from "./his-hope-button.component";

@Component({
  standalone: true,
  imports: [HisHopeButtonComponent],
  template: '<hh-button [loading]="loading">Save</hh-button>',
})
class ButtonHostComponent {
  loading = false;
}

describe("HisHopeButtonComponent", () => {
  let fixture: ComponentFixture<ButtonHostComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ButtonHostComponent],
    }).compileComponents();
    fixture = TestBed.createComponent(ButtonHostComponent);
    fixture.detectChanges();
  });

  it("renders a native button with projected content", () => {
    expect(fixture.nativeElement.querySelector("button").textContent).toContain(
      "Save",
    );
  });

  it("disables the native button while loading", () => {
    fixture.componentInstance.loading = true;
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector("button").disabled).toBeTrue();
    expect(
      fixture.nativeElement.querySelector("button").getAttribute("aria-busy"),
    ).toBe("true");
  });
});
