import { Component, OnInit, OnDestroy, ChangeDetectionStrategy, ChangeDetectorRef, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router, NavigationEnd } from '@angular/router';
import { BreakpointObserver } from '@angular/cdk/layout';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatSidenavModule } from '@angular/material/sidenav';
import { Subject, takeUntil, filter } from 'rxjs';
import { AuthService } from '@core/services/auth.service';
import { RumService } from './monitoring/rum.service';
import { ErrorBarComponent } from '@shared/components/error-bar/error-bar.component';
import {
  HisHopeAuditFeedbackService,
  HisHopeBrandComponent,
  HisHopeI18nService,
  HisHopeLanguageSwitcherComponent,
  HisHopeOfflineBannerComponent,
  HisHopeThemeService,
  HisHopeToastComponent,
  HisHopeTranslatePipe,
  HisHopeWorkspaceHeaderComponent,
} from '@his-hope/frontend-foundation';

@Component({
    selector: 'app-root',
    standalone: true,
    imports: [
        CommonModule, RouterModule,
        MatButtonModule, MatIconModule, MatListModule,
        MatSidenavModule,
        ErrorBarComponent, HisHopeBrandComponent, HisHopeLanguageSwitcherComponent, HisHopeOfflineBannerComponent, HisHopeWorkspaceHeaderComponent, HisHopeToastComponent, HisHopeTranslatePipe,
    ],
    changeDetection: ChangeDetectionStrategy.OnPush,
    template: `
    <mat-sidenav-container class="app-sidenav-container">
      <hh-offline-banner></hh-offline-banner>
      @if (isLoggedIn) {
      <mat-sidenav #sidenav [mode]="sidenavMode" [opened]="isLoggedIn && sidenavOpened">
        <div class="shell-sidebar">
          <div class="shell-sidebar__header">
            <hh-brand [caption]="'app.name' | hhTranslate"></hh-brand>
            <button mat-icon-button class="shell-sidebar__close" (click)="toggleSidenav()"
                    [attr.aria-label]="'app.sidebar.closeMenu' | hhTranslate:'Đóng menu điều hướng'">
              <mat-icon aria-hidden="true">close</mat-icon>
            </button>
          </div>
          <mat-nav-list class="shell-sidebar__nav" aria-label="Điều hướng chính">
            <a mat-list-item routerLink="/dashboard" routerLinkActive="active-link">
              <mat-icon matListItemIcon aria-hidden="true">dashboard</mat-icon>
              <span matListItemTitle>{{ 'navigation.dashboard' | hhTranslate }}</span>
            </a>
            <a mat-list-item routerLink="/patients" routerLinkActive="active-link">
              <mat-icon matListItemIcon aria-hidden="true">people</mat-icon>
              <span matListItemTitle>{{ 'navigation.patients' | hhTranslate }}</span>
            </a>
            <a mat-list-item routerLink="/appointments" routerLinkActive="active-link">
              <mat-icon matListItemIcon aria-hidden="true">calendar_today</mat-icon>
              <span matListItemTitle>{{ 'navigation.appointments' | hhTranslate }}</span>
            </a>
            <a mat-list-item routerLink="/clinical" routerLinkActive="active-link">
              <mat-icon matListItemIcon aria-hidden="true">medical_services</mat-icon>
              <span matListItemTitle>{{ 'navigation.clinical' | hhTranslate }}</span>
            </a>
            <a mat-list-item routerLink="/pharmacy" routerLinkActive="active-link">
              <mat-icon matListItemIcon aria-hidden="true">medication</mat-icon>
              <span matListItemTitle>{{ 'navigation.pharmacy' | hhTranslate }}</span>
            </a>
            <a mat-list-item routerLink="/lab" routerLinkActive="active-link">
              <mat-icon matListItemIcon aria-hidden="true">biotech</mat-icon>
              <span matListItemTitle>{{ 'navigation.laboratory' | hhTranslate }}</span>
            </a>
            <a mat-list-item routerLink="/billing" routerLinkActive="active-link">
              <mat-icon matListItemIcon aria-hidden="true">receipt</mat-icon>
              <span matListItemTitle>{{ 'navigation.billing' | hhTranslate }}</span>
            </a>
            <a mat-list-item routerLink="/admin" routerLinkActive="active-link">
              <mat-icon matListItemIcon aria-hidden="true">settings</mat-icon>
              <span matListItemTitle>{{ 'navigation.administration' | hhTranslate }}</span>
            </a>
          </mat-nav-list>
        </div>
      </mat-sidenav>
      }
      <mat-sidenav-content>
        @if (isLoggedIn && isMobile && !sidenavOpened) {
        <button mat-icon-button class="mobile-menu-button" (click)="openSidenav()" [attr.aria-label]="'app.navigation.openMenu' | hhTranslate:'Mở menu điều hướng'">
          <mat-icon aria-hidden="true">menu</mat-icon>
        </button>
        }
        <app-error-bar></app-error-bar>
        @if (isLoggedIn) {
        <hh-workspace-header />
        }
        <div class="toolbar-actions">
          <button mat-icon-button type="button" (click)="toggleTheme()" [attr.aria-label]="'app.theme.toggle' | hhTranslate:'Đổi giao diện sáng/tối'">
            <mat-icon>brightness_6</mat-icon>
          </button>
          <hh-language-switcher />
        </div>
        <div class="main-content" id="main-content">
          <router-outlet></router-outlet>
        </div>
      </mat-sidenav-content>
    </mat-sidenav-container>
    <hh-toast-outlet />
  `,
    styles: [`
    .app-sidenav-container { min-height: 100dvh; background: var(--bg-warm, #F2F6F3); }
    .app-sidenav-container .mat-drawer-side { width: var(--shell-sidebar-width, 264px); border-right: 1px solid var(--border-default, #EAEAEA); }
    .main-content { min-height: 100dvh; padding: 28px 32px; position: relative; max-width: var(--max-width-container, 1200px); width: 100%; margin: 0 auto; }
    .toolbar-actions { position: absolute; top: 12px; right: 32px; z-index: 5; display: flex; align-items: center; gap: 8px; }
    .mobile-menu-button {
      position: fixed;
      top: 12px;
      left: 12px;
      z-index: 5;
      background: var(--color-primary, #2F6B4A);
      border: 1px solid var(--border-default, #EAEAEA);
      border-radius: 6px;
      color: var(--text-primary, #1A1A1A);
    }
    .shell-sidebar { display: flex; flex-direction: column; height: 100%; background: var(--surface-white); }
    .shell-sidebar__header { display: flex; align-items: center; justify-content: space-between; padding: 22px 20px 16px; flex-shrink: 0; }
    .shell-sidebar__close { color: var(--text-secondary); }
    .shell-sidebar__nav { flex: 1; padding: 4px 8px; overflow-y: auto; }
    .shell-sidebar__nav a { border-radius: 6px; margin-bottom: 2px; color: var(--text-primary); height: 44px; position: relative; transition: background-color 150ms ease; }
    .shell-sidebar__nav a:hover { background: var(--surface-hover); }
    .shell-sidebar__nav a.active-link { background: var(--color-primary-soft); box-shadow: inset 3px 0 0 var(--color-primary); color: var(--color-primary); }
    .shell-sidebar__nav a.active-link:hover { background: var(--color-primary-soft); }
    .shell-sidebar__nav .mat-icon { color: var(--text-secondary); font-size: 20px; width: 20px; height: 20px; }
    .shell-sidebar__nav a.active-link .mat-icon { color: var(--color-primary); }
    :host ::ng-deep .mat-drawer-inner-container { overflow-x: hidden; }
    @media (max-width: 767.98px) {
      .main-content { padding: 20px 16px; }
    }
  `],
})
export class AppComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();
  isLoggedIn = false;
  sidenavOpened = true;
  sidenavMode: 'side' | 'over' = 'side';
  isMobile = false;

  private authService = inject(AuthService);
  private readonly theme = inject(HisHopeThemeService);
  private readonly i18n = inject(HisHopeI18nService);
  private router = inject(Router);
  private auditFeedback = inject(HisHopeAuditFeedbackService);
  private cdr = inject(ChangeDetectorRef);
  private rum = inject(RumService);
  private breakpointObserver = inject(BreakpointObserver);
  private previousUrl = '';

  toggleSidenav(): void {
    this.sidenavOpened = !this.sidenavOpened;
    this.cdr.markForCheck();
  }

  openSidenav(): void {
    this.sidenavOpened = true;
    this.cdr.markForCheck();
  }

  toggleTheme(): void {
    const next = this.theme.resolvedTheme() === 'dark' ? 'light' : 'dark';
    this.theme.setTheme(next);
    this.cdr.markForCheck();
  }

  ngOnInit(): void {
    // Initialise Real User Monitoring (Web Vitals + OpenTelemetry).
    this.rum.initialize();

    this.breakpointObserver.observe('(max-width: 767.98px)')
      .pipe(takeUntil(this.destroy$))
      .subscribe((state) => {
        this.isMobile = state.matches;
        this.sidenavMode = state.matches ? 'over' : 'side';
        this.sidenavOpened = !state.matches;
        this.cdr.markForCheck();
      });

    this.authService.isLoggedIn()
      .pipe(takeUntil(this.destroy$))
      .subscribe((loggedIn) => {
        this.isLoggedIn = loggedIn;
        this.cdr.markForCheck();
      });

    // Also reactively respond to auth state changes
    this.authService.currentUser$
      .pipe(takeUntil(this.destroy$))
      .subscribe((user) => {
        this.isLoggedIn = !!user;
        this.cdr.markForCheck();
      });

    // Navigation audit — log mỗi lần user đổi route
    this.router.events.pipe(
      filter((event): event is NavigationEnd => event instanceof NavigationEnd),
      takeUntil(this.destroy$),
    ).subscribe((event: NavigationEnd) => {
      if (this.previousUrl && this.previousUrl !== event.url) {
        this.auditFeedback.report({
          action: 'navigation.change',
          resource: 'route',
          outcome: 'success',
          message: this.i18n.t('app.navigation.changed', `Đã chuyển từ ${this.previousUrl} đến ${event.url}`, { from: this.previousUrl, to: event.url }),
        });
      }
      this.previousUrl = event.url;
      if (this.isMobile && this.sidenavOpened) {
        this.sidenavOpened = false;
        this.cdr.markForCheck();
      }
    });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
