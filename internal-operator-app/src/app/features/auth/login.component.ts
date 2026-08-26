import { Component, OnDestroy, OnInit, inject } from "@angular/core";
import { CommonModule } from "@angular/common";
import { ActivatedRoute, Router } from "@angular/router";
import { Subject, timer, EMPTY } from "rxjs";
import { exhaustMap, switchMap, take, takeUntil } from "rxjs/operators";
import { MatCardModule } from "@angular/material/card";
import { AuthService } from "../../core/services/auth.service";
import { HisHopeTranslatePipe } from "@his-hope/frontend-foundation/i18n";
import { HisHopeActionButtonComponent } from "@his-hope/frontend-foundation/ui";
import { environment } from "../../../environments/environment";

@Component({
  selector: "app-login",
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule,
    HisHopeActionButtonComponent,
    HisHopeTranslatePipe,
  ],
  template: `
    <div class="login-container">
      <mat-card class="login-card">
        <mat-card-content>
          <div class="login-header">
            <div class="logo">{{ "app.name" | hhTranslate: "His.Hope" }}</div>
            <h2>{{ shellTitle }}</h2>
            <p class="subtitle">
              {{ "customerPortal.signInSubtitle" | hhTranslate: "Sign in with your customer administrator account." }}
            </p>
          </div>
          <div class="login-buttons">
            <hh-action-button
              [disabled]="checkingAuth"
              (pressed)="startLogin()"
              kind="primary"
              icon="login"
              [label]="'customerPortal.signInHisHope' | hhTranslate: 'Sign in with His.Hope'"
            />
          </div>
        </mat-card-content>
      </mat-card>
    </div>
  `,
  styles: [
    `
      .login-container {
        display: flex;
        justify-content: center;
        align-items: center;
        min-height: calc(100dvh - var(--toolbar-height, 64px));
        background: var(--bg-warm);
        padding: var(--space-2xl);
      }
      .login-card {
        max-width: 400px;
        width: 100%;
      }
      .login-header {
        text-align: center;
        margin-bottom: var(--space-3xl);
      }
      .logo {
        font-size: var(--font-size-label);
        font-weight: 700;
        letter-spacing: 0.08em;
        text-transform: uppercase;
        color: var(--text-secondary);
        margin-bottom: var(--space-sm);
      }
      .login-header h2 {
        font-size: var(--font-size-title);
        line-height: 1.25;
        font-weight: 700;
        color: var(--text-primary);
        margin: 0 0 var(--space-2xs);
      }
      .subtitle {
        font-size: var(--font-size-body);
        color: var(--text-secondary);
        margin: 0;
      }
      .login-buttons {
        display: flex;
        flex-direction: column;
        gap: var(--space-md);
      }
    `,
  ],
})
export class LoginComponent implements OnInit, OnDestroy {
  readonly shellTitle = environment.shellTitle;
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly destroy$ = new Subject<void>();
  checkingAuth = true;

  ngOnInit(): void {
    this.authService.isAuthenticated$
      .pipe(takeUntil(this.destroy$))
      .subscribe((isAuth) => {
        this.checkingAuth = false;
        if (isAuth) {
          const returnUrl = this.requestedReturnUrl;
          void this.router.navigateByUrl(
            returnUrl && returnUrl.startsWith("/") ? returnUrl : "/dashboard",
          );
        }
      });

    timer(0, 3000)
      .pipe(
        takeUntil(this.destroy$),
        exhaustMap(() =>
          this.authService.isAuthenticated$.pipe(
            take(1),
            switchMap((isAuth) =>
              isAuth
                ? EMPTY
                : this.authService.trySsoLogin(this.requestedReturnUrl),
            ),
          ),
        ),
      )
      .subscribe();
  }

  startLogin(): void {
    this.authService.login(this.requestedReturnUrl);
  }

  private get requestedReturnUrl(): string | undefined {
    return this.route.snapshot.queryParamMap.get("returnUrl") ?? undefined;
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
