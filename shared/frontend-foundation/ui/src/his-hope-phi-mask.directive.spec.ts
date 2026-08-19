import { Component } from '@angular/core';
import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { HisHopePhiMaskDirective } from './his-hope-phi-mask.directive';

@Component({
  standalone: true,
  imports: [HisHopePhiMaskDirective],
  template: `<span [hhPhiMask]="ssn" [hhPhiMaskAutoHideMs]="1000"></span>`,
})
class HostComponent {
  ssn = '123-45-6789';
}

describe('HisHopePhiMaskDirective', () => {
  let fixture: ComponentFixture<HostComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [HostComponent] });
    fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
  });

  it('masks the value by default', () => {
    const host: HTMLElement = fixture.nativeElement.querySelector('span');
    expect(host.textContent).not.toContain('123-45-6789');
    expect(host.textContent).toMatch(/^\u2022+$/);
  });

  it('reveals the real value on click and re-masks on toggle', () => {
    const host: HTMLElement = fixture.nativeElement.querySelector('span');
    host.click();
    fixture.detectChanges();
    expect(host.textContent).toBe('123-45-6789');
    host.click();
    fixture.detectChanges();
    expect(host.textContent).not.toContain('123-45-6789');
  });

  it('auto re-masks after the configured timeout', fakeAsync(() => {
    const host: HTMLElement = fixture.nativeElement.querySelector('span');
    host.click();
    fixture.detectChanges();
    expect(host.textContent).toBe('123-45-6789');
    tick(1000);
    fixture.detectChanges();
    expect(host.textContent).not.toContain('123-45-6789');
  }));
});
