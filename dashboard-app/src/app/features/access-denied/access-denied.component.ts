import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { HisHopeTranslatePipe } from '@his-hope/frontend-foundation/i18n';

@Component({
  standalone: true,
  selector: 'hh-dashboard-access-denied',
  imports: [RouterLink, HisHopeTranslatePipe],
  template: `
    <main class="access-denied" aria-labelledby="access-denied-title">
      <section class="access-denied__card">
        <p class="access-denied__eyebrow">
          {{ 'common.accessDenied' | hhTranslate: 'Access denied' }}
        </p>
        <h1 id="access-denied-title">
          {{ 'forbidden.title' | hhTranslate: 'Bạn không có quyền truy cập' }}
        </h1>
        <p>
          {{
            'forbidden.subtitle'
              | hhTranslate: 'Tài khoản của bạn không được cấp quyền dashboard.'
          }}
        </p>
        <a routerLink="/auth/login">{{
          'common.backToLogin' | hhTranslate: 'Quay lại đăng nhập'
        }}</a>
      </section>
    </main>
  `,
  styles: [
    `
      :host {
        display: block;
        min-height: 100dvh;
        background: var(--bg-warm);
        color: var(--text-primary);
      }
      .access-denied {
        display: grid;
        place-items: center;
        min-height: 100dvh;
        padding: var(--space-6);
      }
      .access-denied__card {
        width: min(100%, 34rem);
        padding: var(--space-8);
        border: 1px solid var(--border-default);
        border-radius: var(--radius-card);
        background: var(--surface-white);
        box-shadow: var(--shadow-dropdown);
      }
      .access-denied__eyebrow {
        margin: 0 0 var(--space-2);
        color: var(--text-secondary);
        font-size: var(--font-size-sm);
      }
      h1 {
        margin: 0 0 var(--space-3);
        font-size: var(--font-size-title);
      }
      p {
        color: var(--text-secondary);
      }
      a {
        color: var(--color-primary);
        font-weight: 600;
      }
    `,
  ],
})
export class AccessDeniedComponent {}
