import { Component } from "@angular/core";
import { ComponentFixture, TestBed } from "@angular/core/testing";
import { HisHopeWorkflowStepperComponent } from "./his-hope-workflow-stepper.component";

@Component({
  standalone: true,
  imports: [HisHopeWorkflowStepperComponent],
  template: `
    <hh-workflow-stepper
      ariaLabel="Production order workflow"
      [steps]="steps"
    />
  `,
})
class HostComponent {
  steps = [
    { key: "Draft", label: "Draft", state: "complete" as const },
    { key: "Planned", label: "Planned", state: "current" as const },
    { key: "Released", label: "Released", state: "upcoming" as const },
  ];
}

describe("HisHopeWorkflowStepperComponent", () => {
  let fixture: ComponentFixture<HostComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [HostComponent] });
    fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
  });

  it("renders workflow steps with current step marked", () => {
    const items = fixture.nativeElement.querySelectorAll(".hh-workflow-stepper__item");
    expect(items.length).toBe(3);
    expect(items[1].getAttribute("aria-current")).toBe("step");
    expect(items[1].textContent).toContain("Planned");
  });

  it("falls back to a single current step when only currentStatus is provided", () => {
    TestBed.resetTestingModule();
    @Component({
      standalone: true,
      imports: [HisHopeWorkflowStepperComponent],
      template: `<hh-workflow-stepper currentStatus="Approved" />`,
    })
    class FallbackHostComponent {}

    TestBed.configureTestingModule({ imports: [FallbackHostComponent] });
    const fallbackFixture = TestBed.createComponent(FallbackHostComponent);
    fallbackFixture.detectChanges();

    const labels = fallbackFixture.nativeElement.querySelectorAll(".hh-workflow-stepper__label");
    expect(labels.length).toBe(1);
    expect(labels[0].textContent).toContain("Approved");
  });
});
