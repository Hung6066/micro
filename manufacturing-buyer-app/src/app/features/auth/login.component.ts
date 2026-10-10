import { Component, OnDestroy, OnInit, inject } from "@angular/core";
import { CommonModule } from "@angular/common";
import { ActivatedRoute, Router, RouterLink } from "@angular/router";
import { Subject, timer, EMPTY } from "rxjs";
import { exhaustMap, switchMap, take, takeUntil } from "rxjs/operators";
import { AuthService } from "../../core/services/auth.service";
import { HisHopeTranslatePipe } from "@his-hope/frontend-foundation/i18n";

@Component({
  selector: "app-login",
  standalone: true,
  imports: [CommonModule, RouterLink, HisHopeTranslatePipe],
  template: `
    <section class="login-page">
      <div class="login-card fx-card">
<p class="eyebrow">{{ 'buyer.login.eyebrow' | hhTranslate }}</p>
        <h1>{{ 'buyer.login.title' | hhTranslate }}</h1>
        <p>{{ 'buyer.login.description' | hhTranslate: 'B2B dried fruit — pilot account: {{account}}' : { account: 'buyer.pilot' } }}</p>
        <button type="button" class="fx-btn-primary" [disabled]="checkingAuth" (click)="startLogin()">
          {{ 'buyer.login.action' | hhTranslate }}
        </button>
        <a routerLink="/home">{{ 'buyer.back.home' | hhTranslate }}</a>
      </div>
    </section>
  `,
  styles: [
    `
      .login-page {
        min-height: calc(100dvh - 220px);
        display: grid;
        place-items: center;
        padding: 2rem 1rem;
      }
      .login-card {
        width: min(460px, 100%);
        padding: 2rem;
        text-align: center;
      }
      .eyebrow {
        margin: 0 0 0.5rem;
        color: var(--color-primary);
        font-weight: var(--font-weight-extrabold);
        text-transform: uppercase;
        letter-spacing: 0.08em;
        font-size: var(--font-size-caption);
      }
      h1 { margin: 0 0 0.75rem; font-size: var(--font-size-display); }
      p { margin: 0 0 1.25rem; color: var(--text-secondary); line-height: 1.6; }
      code { background: var(--color-primary-soft); padding: 0.1rem 0.35rem; border-radius: 4px; }
      a { display: inline-block; margin-top: 1rem; color: var(--color-primary); font-weight: var(--font-weight-bold); }
    `,
  ],
})
export class LoginComponent implements OnInit, OnDestroy {
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
          const returnUrl = this.route.snapshot.queryParamMap.get("returnUrl") ?? "/catalog";
          void this.router.navigateByUrl(returnUrl.startsWith("/") ? returnUrl : "/catalog");
        }
      });

    timer(0, 3000)
      .pipe(
        takeUntil(this.destroy$),
        exhaustMap(() =>
          this.authService.isAuthenticated$.pipe(
            take(1),
            switchMap((isAuth) =>
              isAuth ? EMPTY : this.authService.trySsoLogin(this.requestedReturnUrl),
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
