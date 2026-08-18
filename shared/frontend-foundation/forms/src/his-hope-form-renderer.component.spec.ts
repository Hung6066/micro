import { FormGroup } from '@angular/forms';
import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { HisHopeFormRendererComponent } from './his-hope-form-renderer.component';
import { createHisHopeFormGroup, HisHopeFormFieldSchema } from './his-hope-form-schema';

@Component({ standalone: true, imports: [HisHopeFormRendererComponent], template: '<hh-form-renderer [fields]="fields" [form]="form" (submitted)="submitted = $event" />' })
class HostComponent {
  readonly fields: readonly HisHopeFormFieldSchema<unknown>[] = [{ key: 'name', label: 'Name', initialValue: '', required: true }];
  readonly form: FormGroup = createHisHopeFormGroup({ fields: { name: this.fields[0] } });
  submitted: Record<string, unknown> | undefined;
}

describe('HisHopeFormRendererComponent', () => {
  it('marks required controls invalid and emits valid values', () => {
    const fixture = TestBed.configureTestingModule({ imports: [HostComponent] }).createComponent(HostComponent);
    fixture.detectChanges();
    const host = fixture.componentInstance;
    expect(host.form.invalid).toBeTrue();
    host.form.get('name')?.setValue('Alice');
    fixture.nativeElement.querySelector('form').dispatchEvent(new Event('submit'));
    expect(host.submitted).toEqual({ name: 'Alice' });
  });
});
