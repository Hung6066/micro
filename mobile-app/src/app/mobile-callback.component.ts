import { Component, OnInit, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { HisHopeStateComponent } from '@his-hope/frontend-foundation';
import { catchError, of, take } from 'rxjs';
import { MobileAuthService } from './core/auth.service';

@Component({
  standalone: true,
  imports: [HisHopeStateComponent],
  template: `
    @if (status() === 'loading') {
      <hh-state kind="loading" message="Completing secure sign-in..." />
    } @else if (status() === 'success') {
      <section class="callback-success" role="status" aria-live="polite">
        <span aria-hidden="true">&#10003;</span>
        <p>Signed in successfully. Opening dashboard...</p>
      </section>
    } @else {
      <hh-state kind="error" message="Sign-in could not be completed. Please try again." />
    }
  `,
})
export class MobileCallbackComponent implements OnInit {
  private readonly auth = inject(MobileAuthService);
  private readonly router = inject(Router);
  readonly status = signal<'loading' | 'success' | 'error'>('loading');

  ngOnInit(): void {
    this.auth.completeCallback().pipe(
      take(1),
      catchError(() => of(false)),
    ).subscribe(isAuthenticated => {
      if (!isAuthenticated) {
        this.status.set('error');
        return;
      }
      this.status.set('success');
      setTimeout(() => void this.router.navigateByUrl('/admin/dashboard'), 450);
    });
  }
}
