import { OverlayModule } from '@angular/cdk/overlay';
import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { HisHopeDialogService } from './his-hope-dialog.service';
import {
  HisHopeSessionTimeoutDialogComponent,
  HisHopeSessionTimeoutDialogData,
  HisHopeSessionTimeoutResult,
} from './his-hope-session-timeout-dialog.component';

describe('HisHopeSessionTimeoutDialogComponent', () => {
  let service: HisHopeDialogService;

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [OverlayModule] });
    service = TestBed.inject(HisHopeDialogService);
  });

  it('counts down and closes with "signedOut" when time expires', fakeAsync(() => {
    const ref = service.open<
      HisHopeSessionTimeoutDialogComponent,
      HisHopeSessionTimeoutDialogData,
      HisHopeSessionTimeoutResult
    >(HisHopeSessionTimeoutDialogComponent, {
      data: { expiresAt: new Date(Date.now() + 2000) },
      disableClose: true,
    });
    let result: HisHopeSessionTimeoutResult | undefined;
    ref.afterClosed().subscribe((value) => (result = value));
    tick(1000);
    expect(document.body.textContent).toContain('1s');
    tick(1500);
    expect(result).toBe('signedOut');
  }));

  it('closes with "extended" when the user stays signed in', fakeAsync(() => {
    const ref = service.open<
      HisHopeSessionTimeoutDialogComponent,
      HisHopeSessionTimeoutDialogData,
      HisHopeSessionTimeoutResult
    >(HisHopeSessionTimeoutDialogComponent, {
      data: { expiresAt: new Date(Date.now() + 30000) },
    });
    let result: HisHopeSessionTimeoutResult | undefined;
    ref.afterClosed().subscribe((value) => (result = value));
    tick(0);
    const staySignedInButton = Array.from(
      document.querySelectorAll<HTMLButtonElement>('button'),
    ).find((button) => button.textContent?.includes('Stay signed in'));
    staySignedInButton?.click();
    expect(result).toBe('extended');
  }));
});
