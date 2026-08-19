import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormsModule } from '@angular/forms';
import { HisHopeChipsComponent } from './his-hope-chips.component';

@Component({
  standalone: true,
  imports: [FormsModule, HisHopeChipsComponent],
  template: `<hh-chips [(ngModel)]="tags" label="Tags" />`,
})
class HostComponent {
  tags: string[] = [];
}

describe('HisHopeChipsComponent', () => {
  let fixture: ComponentFixture<HostComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [HostComponent] });
    fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
  });

  it('adds a chip on Enter and updates the ngModel-bound value', () => {
    const input: HTMLInputElement = fixture.nativeElement.querySelector('.hh-chips__input');
    input.value = 'urgent';
    input.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter' }));
    fixture.detectChanges();
    expect(fixture.componentInstance.tags).toEqual(['urgent']);
    expect(input.value).toBe('');
  });

  it('does not add duplicate chips', () => {
    fixture.componentInstance.tags = ['urgent'];
    fixture.detectChanges();
    const input: HTMLInputElement = fixture.nativeElement.querySelector('.hh-chips__input');
    input.value = 'urgent';
    input.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter' }));
    fixture.detectChanges();
    expect(fixture.componentInstance.tags).toEqual(['urgent']);
  });

  it('removes a chip when its remove button is clicked', () => {
    fixture.componentInstance.tags = ['urgent', 'follow-up'];
    fixture.detectChanges();
    const removeButton: HTMLButtonElement = fixture.nativeElement.querySelector(
      '.hh-chips__remove',
    );
    removeButton.click();
    fixture.detectChanges();
    expect(fixture.componentInstance.tags).toEqual(['follow-up']);
  });

  it('removes the last chip on Backspace when the input is empty', () => {
    fixture.componentInstance.tags = ['urgent', 'follow-up'];
    fixture.detectChanges();
    const input: HTMLInputElement = fixture.nativeElement.querySelector('.hh-chips__input');
    input.dispatchEvent(new KeyboardEvent('keydown', { key: 'Backspace' }));
    fixture.detectChanges();
    expect(fixture.componentInstance.tags).toEqual(['urgent']);
  });
});
