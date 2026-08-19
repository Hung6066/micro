import { OverlayModule } from '@angular/cdk/overlay';
import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormsModule } from '@angular/forms';
import {
  HisHopeMultiSelectComponent,
  HisHopeMultiSelectOption,
} from './his-hope-multi-select.component';

@Component({
  standalone: true,
  imports: [OverlayModule, FormsModule, HisHopeMultiSelectComponent],
  template: `<hh-multi-select [(ngModel)]="roles" [options]="options" label="Roles" />`,
})
class HostComponent {
  roles: string[] = [];
  options: HisHopeMultiSelectOption[] = [
    { value: 'admin', label: 'Administrator' },
    { value: 'nurse', label: 'Nurse' },
    { value: 'locked', label: 'Locked option', disabled: true },
  ];
}

describe('HisHopeMultiSelectComponent', () => {
  let fixture: ComponentFixture<HostComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [HostComponent] });
    fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
  });

  afterEach(() =>
    document.querySelectorAll('.hh-multi-select__panel').forEach((el) => el.remove()),
  );

  it('opens the listbox panel on click', () => {
    const host: HTMLElement = fixture.nativeElement.querySelector('hh-multi-select');
    host.click();
    fixture.detectChanges();
    expect(document.querySelector('[role="listbox"][aria-multiselectable="true"]')).toBeTruthy();
  });

  it('toggles an option without closing the panel and updates the value', () => {
    const host: HTMLElement = fixture.nativeElement.querySelector('hh-multi-select');
    host.click();
    fixture.detectChanges();
    const option = document.querySelector('[role="option"]') as HTMLElement;
    option.click();
    fixture.detectChanges();
    expect(fixture.componentInstance.roles).toEqual(['admin']);
    expect(document.querySelector('[role="listbox"]')).toBeTruthy();
  });

  it('shows a count summary once more than two options are selected', () => {
    fixture.componentInstance.roles = ['admin', 'nurse'];
    fixture.detectChanges();
    const host: HTMLElement = fixture.nativeElement.querySelector('hh-multi-select');
    expect(host.textContent).toContain('Administrator, Nurse');
  });

  it('does not toggle a disabled option', () => {
    const host: HTMLElement = fixture.nativeElement.querySelector('hh-multi-select');
    host.click();
    fixture.detectChanges();
    const disabledOption = Array.from(
      document.querySelectorAll<HTMLElement>('[role="option"]'),
    ).find((el) => el.textContent?.includes('Locked'));
    disabledOption?.click();
    fixture.detectChanges();
    expect(fixture.componentInstance.roles).toEqual([]);
  });
});
