import { Component, ChangeDetectionStrategy } from "@angular/core";
import { CommonModule } from "@angular/common";
import { MatIconModule } from "@angular/material/icon";
import { MatButtonModule } from "@angular/material/button";
import { RouterModule } from "@angular/router";
import { HisHopeTranslatePipe } from "@his-hope/frontend-foundation/i18n";

@Component({
  selector: "app-forgot-password",
  standalone: true,
  imports: [
    CommonModule,
    MatIconModule,
    MatButtonModule,
    RouterModule,
    HisHopeTranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="auth-placeholder">
      <mat-icon>lock_reset</mat-icon>
      <h1>
        {{ "auth.forgotPasswordTitle" | hhTranslate: "Khôi phục mật khẩu" }}
      </h1>
      <p>
        {{
          "auth.forgotPasswordComingSoon"
            | hhTranslate
              : "Chức năng đặt lại mật khẩu sẽ được bổ sung sau. Nếu cần hỗ trợ, vui lòng liên hệ quản trị hệ thống."
        }}
      </p>
      <button mat-stroked-button routerLink="/auth/login">
        {{ "auth.backToLogin" | hhTranslate: "Quay lại đăng nhập" }}
      </button>
    </div>
  `,
  styles: [
    `
      .auth-placeholder {
        display: flex;
        flex-direction: column;
        align-items: center;
        justify-content: center;
        min-height: 100dvh;
        padding: 24px;
        text-align: center;
        background: var(--bg-warm, #f7f6f3);
      }
      .auth-placeholder mat-icon {
        font-size: 64px;
        width: 64px;
        height: 64px;
        color: var(--color-primary, #2f6b4a);
        margin-bottom: 16px;
      }
      .auth-placeholder h1 {
        margin: 0 0 8px 0;
        font-size: var(--font-size-title, 24px);
        line-height: 1.25;
        letter-spacing: 0;
        font-weight: 700;
      }
      .auth-placeholder p {
        color: var(--text-secondary, #787774);
        margin: 0 0 24px 0;
        max-width: 28rem;
      }
    `,
  ],
})
export class ForgotPasswordComponent {}
