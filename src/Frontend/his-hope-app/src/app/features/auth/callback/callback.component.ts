import { Component, OnInit, ChangeDetectionStrategy, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { AuthService } from '@core/services/auth.service';

@Component({
  selector: 'app-callback',
  standalone: true,
  imports: [CommonModule, MatProgressSpinnerModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="callback-container">
      <mat-spinner diameter="40" aria-label="Completing sign in"></mat-spinner>
      <p>Completing sign in...</p>
    </div>
  `,
  styles: [`
    .callback-container {
      min-height: 100dvh;
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      gap: 16px;
      background: var(--bg-warm, #F7F6F3);
      color: var(--text-secondary, #787774);
    }
  `],
})
export class CallbackComponent implements OnInit {
  private authService = inject(AuthService);
  private router = inject(Router);

  ngOnInit(): void {
    this.authService.handleCallback().subscribe({
      next: (isAuthenticated) => {
        if (isAuthenticated) {
          const returnUrl = sessionStorage.getItem('oidc_returnUrl');
          sessionStorage.removeItem('oidc_returnUrl');
          this.router.navigateByUrl(returnUrl || '/dashboard');
        } else {
          this.router.navigate(['/auth/login']);
        }
      },
      error: () => {
        this.router.navigate(['/auth/login']);
      },
    });
  }
}
