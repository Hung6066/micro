// @ts-nocheck
import { Component, ChangeDetectionStrategy } from '@angular/core';

@Component({
  selector: 'app-forgot-password',
  standalone: false,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="auth-placeholder">
      <mat-icon>lock_reset</mat-icon>
      <h1>Forgot Password</h1>
      <p>Password reset functionality is coming soon.</p>
      <button mat-stroked-button routerLink="/auth/login">Back to Login</button>
    </div>
  `,
  styles: [`
    .auth-placeholder { display: flex; flex-direction: column; align-items: center; justify-content: center; height: 100vh; padding: 24px; text-align: center; background: var(--bg-warm, #F7F6F3); }
    .auth-placeholder mat-icon { font-size: 64px; width: 64px; height: 64px; color: var(--color-primary, #2F6B4A); margin-bottom: 16px; }
    .auth-placeholder h1 { margin: 0 0 8px 0; }
    .auth-placeholder p { color: var(--text-secondary, #787774); margin: 0 0 24px 0; }
  `],
})
export class ForgotPasswordComponent {}
