import {
  ChangeDetectionStrategy,
  Component,
  OnDestroy,
  OnInit,
  inject,
  signal,
} from '@angular/core';
import { HisHopeTranslatePipe } from '@his-hope/frontend-foundation/i18n';
import { HIS_HOPE_DIALOG_DATA, HisHopeDialogRef } from './his-hope-dialog.service';

export type HisHopeSessionTimeoutResult = 'extended' | 'signedOut';

export interface HisHopeSessionTimeoutDialogData {
  expiresAt: Date;
}

/**
 * Presentational countdown dialog for idle/session-expiry warnings. The
 * hosting app owns idle detection and token refresh; open this via
 * `HisHopeDialogService.open(HisHopeSessionTimeoutDialogComponent, { data: { expiresAt }, disableClose: true })`
 * and react to `HisHopeDialogRef.afterClosed()`.
 */
@Component({
  selector: 'hh-session-timeout-dialog',
  standalone: true,
  imports: [HisHopeTranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="hh-session-timeout" role="alertdialog" aria-live="assertive">
      <div class="hh-session-timeout__icon material-icons" aria-hidden="true">
        schedule
      </div>
      <h2>{{ 'common.sessionExpiringTitle' | hhTranslate: 'Your session is about to expire' }}</h2>
      <p>
        {{
          'common.sessionExpiringMessage'
            | hhTranslate: 'For your security, you will be signed out in'
        }}
        <strong>{{ secondsRemaining() }}s</strong>
      </p>
      <div class="hh-session-timeout__actions">
        <button type="button" class="hh-button hh-button--secondary" (click)="signOut()">
          {{ 'common.signOutNow' | hhTranslate: 'Sign out now' }}
        </button>
        <button type="button" class="hh-button hh-button--primary" (click)="staySignedIn()">
          {{ 'common.staySignedIn' | hhTranslate: 'Stay signed in' }}
        </button>
      </div>
    </div>
  `,
  styles: [
    `
      .hh-session-timeout {
        display: flex;
        flex-direction: column;
        align-items: center;
        gap: var(--space-sm);
        max-width: 360px;
        padding: var(--page-padding-block) var(--space-2xl);
        text-align: center;
        color: var(--text-primary);
        background: var(--surface-white);
      }
      .hh-session-timeout__icon {
        font-size: var(--font-size-display-md);
        color: var(--color-warning, #b45309);
      }
      h2 {
        margin: var(--space-2xs) 0 0;
        font-size: var(--font-size-title);
      }
      p {
        margin: 0;
        color: var(--text-secondary);
      }
      .hh-session-timeout__actions {
        display: flex;
        gap: var(--space-md);
        margin-top: var(--space-md);
      }
      .hh-button {
        min-height: var(--button-height);
        padding: 0 var(--space-lg);
        border: 1px solid transparent;
        border-radius: var(--radius-button);
        font: inherit;
        font-weight: var(--button-font-weight);
        cursor: pointer;
      }
      .hh-button--primary {
        border-color: var(--button-primary-border, transparent);
        background: var(--button-primary-bg);
        color: var(--button-primary-text);
      }
      .hh-button--secondary {
        border-color: var(--button-secondary-border);
        background: var(--button-secondary-bg);
        color: var(--button-secondary-text);
      }
    `,
  ],
})
export class HisHopeSessionTimeoutDialogComponent implements OnInit, OnDestroy {
  readonly secondsRemaining = signal(0);
  private readonly data = inject(HIS_HOPE_DIALOG_DATA) as HisHopeSessionTimeoutDialogData;
  private readonly dialogRef = inject(HisHopeDialogRef<HisHopeSessionTimeoutResult>);
  private timer: ReturnType<typeof setInterval> | null = null;

  ngOnInit(): void {
    this.tick();
    this.timer = setInterval(() => this.tick(), 1000);
  }

  private tick(): void {
    const remaining = Math.max(0, Math.round((this.data.expiresAt.getTime() - Date.now()) / 1000));
    this.secondsRemaining.set(remaining);
    if (remaining <= 0) this.signOut();
  }

  staySignedIn(): void {
    this.dialogRef.close('extended');
  }

  signOut(): void {
    this.dialogRef.close('signedOut');
  }

  ngOnDestroy(): void {
    if (this.timer) clearInterval(this.timer);
  }
}
