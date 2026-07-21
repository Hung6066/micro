import { Component, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { RouterModule } from '@angular/router';

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [CommonModule, MatIconModule, MatButtonModule, RouterModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="auth-placeholder">
      <mat-icon>person_add</mat-icon>
      <h1>Đăng ký tài khoản</h1>
      <p>Chức năng đăng ký sẽ mở sau. Hiện tại, hãy dùng tài khoản được cấp để đăng nhập.</p>
      <button mat-stroked-button routerLink="/auth/login">Quay lại đăng nhập</button>
    </div>
  `,
  styles: [`
    .auth-placeholder { display: flex; flex-direction: column; align-items: center; justify-content: center; min-height: 100dvh; padding: 24px; text-align: center; background: var(--bg-warm, #F7F6F3); }
    .auth-placeholder mat-icon { font-size: 64px; width: 64px; height: 64px; color: var(--color-primary, #2F6B4A); margin-bottom: 16px; }
    .auth-placeholder h1 { margin: 0 0 8px 0; font-size: 28px; line-height: 1.1; letter-spacing: -0.02em; }
    .auth-placeholder p { color: var(--text-secondary, #787774); margin: 0 0 24px 0; max-width: 28rem; }
  `],
})
export class RegisterComponent {}
