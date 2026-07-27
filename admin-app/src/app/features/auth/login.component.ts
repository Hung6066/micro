import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { Subject, timer } from 'rxjs';
import { takeUntil } from 'rxjs/operators';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatIconModule } from '@angular/material/icon';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, MatCardModule, MatButtonModule, MatProgressSpinnerModule, MatIconModule],
  template: `
    <div class="login-container">
      <mat-card class="login-card">
        <mat-card-content>
          <div class="login-header">
            <div class="logo">His.Hope</div>
            <h2>Admin</h2>
            <p class="subtitle">Sign in to manage OIDC resources</p>
          </div>
          <div class="login-buttons">
            <button mat-raised-button color="primary" class="full-width" (click)="oidcLogin()" [disabled]="checkingAuth">
              @if (checkingAuth) { <mat-spinner diameter="20" class="btn-spinner"></mat-spinner> }
              @if (!checkingAuth) { <mat-icon>login</mat-icon> }
              @if (!checkingAuth) { Sign in with His.Hope }
            </button>
          </div>
        </mat-card-content>
      </mat-card>
    </div>
  `,
  styles: [`
    .login-container { display: flex; justify-content: center; align-items: center; min-height: 100vh; background: var(--bg-warm); padding: 24px; }
    .login-card { max-width: 400px; width: 100%; }
    .login-header { text-align: center; margin-bottom: 32px; }
    .logo { font-size: var(--font-size-label); font-weight: 700; letter-spacing: 0.08em; text-transform: uppercase; color: var(--text-secondary); margin-bottom: 8px; }
    .login-header h2 { font-size: var(--font-size-title); line-height: 1.25; font-weight: 700; color: var(--text-primary); margin: 0 0 4px; letter-spacing: 0; }
    .subtitle { font-size: var(--font-size-body); color: var(--text-secondary); margin: 0; }
    .login-buttons { display: flex; flex-direction: column; gap: 12px; }
    .full-width { width: 100%; min-height: var(--button-height, 40px); font-size: var(--button-font-size, 14px); font-weight: var(--button-font-weight, 600); letter-spacing: 0; display: flex; align-items: center; justify-content: center; gap: 8px; }
    .btn-spinner { display: inline-block; margin: 0 auto; }
  `],
})
export class LoginComponent implements OnInit, OnDestroy {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly destroy$ = new Subject<void>();
  checkingAuth = true;

  ngOnInit(): void {
    this.authService.isAuthenticated$.pipe(takeUntil(this.destroy$)).subscribe(isAuth => {
      this.checkingAuth = false;
      if (isAuth) this.router.navigate(['/clients']);
    });

    timer(0, 3000).pipe(takeUntil(this.destroy$)).subscribe(() => {
      this.authService.trySsoLogin().pipe(takeUntil(this.destroy$)).subscribe();
    });
  }

  oidcLogin(): void { this.authService.oidcLogin(); }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
