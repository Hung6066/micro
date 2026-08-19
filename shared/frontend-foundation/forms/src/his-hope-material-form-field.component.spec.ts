import { Component } from "@angular/core";
import { FormControl, ReactiveFormsModule, Validators } from "@angular/forms";
import { TestBed } from "@angular/core/testing";
import { HisHopeMaterialFormFieldComponent } from "./his-hope-material-form-field.component";

@Component({
  standalone: true,
  imports: [ReactiveFormsModule, HisHopeMaterialFormFieldComponent],
  template: ` <hh-mat-form-field [control]="control" label="Name" /> `,
})
class HostComponent {
  readonly control = new FormControl("", { validators: Validators.required });
}

describe("HisHopeMaterialFormFieldComponent", () => {
  it("renders the shared label and localized required error", () => {
    const fixture = TestBed.configureTestingModule({
      imports: [HostComponent],
    }).createComponent(HostComponent);
    fixture.detectChanges();

    const control = fixture.componentInstance.control;
    control.markAsTouched();
    fixture.detectChanges();

    expect(
      fixture.nativeElement.querySelector("mat-label").textContent,
    ).toContain("Name");
    expect(
      fixture.nativeElement.querySelector("mat-error").textContent,
    ).toContain("This field is required.");
  });
});
