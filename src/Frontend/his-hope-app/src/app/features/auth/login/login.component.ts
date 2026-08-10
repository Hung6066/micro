import { Component, OnDestroy, OnInit, ChangeDetectionStrategy, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { EMPTY, Subject, timer } from 'rxjs';
import { exhaustMap, takeUntil } from 'rxjs/operators';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDividerModule } from '@angular/material/divider';
import { HisHopeTranslatePipe } from '@his-hope/frontend-foundation';
import { AuthService } from '@core/services/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [
    CommonModule, RouterModule,
    MatCardModule, MatButtonModule, MatIconModule, MatDividerModule,
    HisHopeTranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="login-container">
      <mat-card class="login-card">
        <mat-card-header>
          <span class="card-logo" aria-hidden="true"></span>
          <mat-card-title>His.Hope</mat-card-title>
          <mat-card-subtitle>{{ 'auth.signInContinue' | hhTranslate:'Sign in to continue' }}</mat-card-subtitle>
        </mat-card-header>

        <mat-card-content>
          <button mat-raised-button color="primary" class="full-width oidc-btn" (click)="signIn()">
            <mat-icon>login</mat-icon>
            {{ 'auth.signInWithHisHope' | hhTranslate:'Sign in with His.Hope' }}
          </button>

          <mat-divider class="divider"><span>{{ 'auth.or' | hhTranslate:'or' }}</span></mat-divider>

          <button mat-stroked-button class="full-width oidc-btn" (click)="signInGoogle()">
            <mat-icon>account_circle</mat-icon>
            {{ 'auth.continueWithGoogle' | hhTranslate:'Continue with Google' }}
          </button>

          <button mat-stroked-button class="full-width oidc-btn" (click)="signInMicrosoft()">
            <mat-icon>workspaces</mat-icon>
            {{ 'auth.continueWithMicrosoft' | hhTranslate:'Continue with Microsoft' }}
          </button>
        </mat-card-content>
      </mat-card>
    </div>
  `,
  styles: [`
    .login-container { min-height: 100dvh; display: grid; place-items: center; padding: 24px; background: var(--bg-warm, #F7F6F3); }
    .login-card { width: min(100%, 420px); }
    .card-logo {
      position: relative;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 48px;
      height: 48px;
      margin: 0 auto 16px;
      border-radius: 10px;
      background: var(--color-primary, #2F6B4A);
      box-shadow: inset 0 0 0 1px rgba(255, 255, 255, 0.18);
    }
    .card-logo::before,
    .card-logo::after {
      content: '';
      position: absolute;
      border-radius: 2px;
      background: #FFFFFF;
    }
    .card-logo::before {
      width: 24px;
      height: 6px;
    }
    .card-logo::after {
      width: 6px;
      height: 24px;
    }
    mat-card-header { flex-direction: column; align-items: center; text-align: center; margin-bottom: 24px; }
    mat-card-title { font-size: var(--font-size-title, 24px); line-height: 1.25; letter-spacing: 0; font-weight: 700; }
    mat-card-subtitle { color: var(--text-secondary, #787774); }
    mat-card-content { padding-top: 0; display: flex; flex-direction: column; gap: 16px; }
    .full-width { width: 100%; }
    .oidc-btn { display: flex; align-items: center; justify-content: center; gap: 8px; min-height: var(--button-height, 40px); padding: 0 16px; font-size: var(--button-font-size, 14px); font-weight: var(--button-font-weight, 600); letter-spacing: 0; }
    .divider { margin: 4px 0; }
    .divider span { background: #fff; padding: 0 12px; color: var(--text-secondary, #787774); font-size: 13px; }
  `],
})
export class LoginComponent implements OnInit, OnDestroy {
  private authService = inject(AuthService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private readonly destroy$ = new Subject<void>();

  ngOnInit(): void {
    this.authService.isLoggedIn().pipe(takeUntil(this.destroy$)).subscribe(isAuth => {
      if (isAuth) {
        const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl');
        this.router.navigateByUrl(returnUrl || '/dashboard');
      }
    });

    timer(1500, 5000).pipe(
      exhaustMap(() => {
        if (!this.router.url.includes('/auth/login')) {
          return EMPTY;
        }
        const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl') || undefined;
        return this.authService.trySsoLogin(returnUrl);
      }),
      takeUntil(this.destroy$),
    ).subscribe();
  }

  signIn(): void {
    const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl') || undefined;
    this.authService.oidcLogin(returnUrl);
  }

  signInGoogle(): void {
    const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl') || undefined;
    if (returnUrl) sessionStorage.setItem('oidc_returnUrl', returnUrl);
    this.authService.oidcLogin(returnUrl);
  }

  signInMicrosoft(): void {
    const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl') || undefined;
    if (returnUrl) sessionStorage.setItem('oidc_returnUrl', returnUrl);
    this.authService.oidcLogin(returnUrl);
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
