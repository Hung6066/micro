import { Overlay, OverlayModule } from '@angular/cdk/overlay';
import { Component, inject } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { HIS_HOPE_DIALOG_DATA, HisHopeDialogRef, HisHopeDialogService } from './his-hope-dialog.service';

@Component({
  standalone: true,
  template: `<p>{{ data }}</p><button (click)="dialogRef.close('result')">close</button>`,
})
class TestDialogContentComponent {
  readonly dialogRef = inject(HisHopeDialogRef);
  readonly data = inject(HIS_HOPE_DIALOG_DATA);
}

describe('HisHopeDialogService', () => {
  let service: HisHopeDialogService;

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [OverlayModule] });
    service = TestBed.inject(HisHopeDialogService);
  });

  it('renders the given component with injected data and role="dialog"', () => {
    const ref = service.open(TestDialogContentComponent, { data: 'hello', ariaLabel: 'Test dialog' });
    const overlay = TestBed.inject(Overlay);
    expect(overlay).toBeTruthy();
    const panel = document.querySelector('.hh-dialog-cdk-panel');
    expect(panel).toBeTruthy();
    expect(panel?.parentElement?.getAttribute('aria-label')).toBe('Test dialog');
    expect(document.body.textContent).toContain('hello');
    ref.close();
  });

  it('emits the close result via afterClosed()', (done) => {
    const ref = service.open(TestDialogContentComponent, { data: 'hello' });
    ref.afterClosed().subscribe((result) => {
      expect(result).toBe('done');
      done();
    });
    ref.close('done');
  });

  it('closes on Escape keydown by default', () => {
    const ref = service.open(TestDialogContentComponent, { data: 'hello' });
    let closed = false;
    ref.afterClosed().subscribe(() => (closed = true));
    const panel = document.querySelector('.hh-dialog-cdk-panel') as HTMLElement;
    panel.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    expect(closed).toBe(true);
  });

  it('does not close on Escape when disableClose is set', () => {
    const ref = service.open(TestDialogContentComponent, { data: 'hello', disableClose: true });
    let closed = false;
    ref.afterClosed().subscribe(() => (closed = true));
    const panel = document.querySelector('.hh-dialog-cdk-panel') as HTMLElement;
    panel.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    expect(closed).toBe(false);
    ref.close();
  });
});
