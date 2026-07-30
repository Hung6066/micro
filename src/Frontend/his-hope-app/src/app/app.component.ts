import { Component, OnInit, OnDestroy, ChangeDetectionStrategy, ChangeDetectorRef, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router, NavigationEnd } from '@angular/router';
import { BreakpointObserver } from '@angular/cdk/layout';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSidenavModule } from '@angular/material/sidenav';
import { Subject, takeUntil, filter } from 'rxjs';
import { AuthService } from '@core/services/auth.service';
import { RumService } from './monitoring/rum.service';
import { SidebarComponent } from '@shared/components/sidebar/sidebar.component';
import { ErrorBarComponent } from '@shared/components/error-bar/error-bar.component';
import {
  HisHopeAuditFeedbackService,
  HisHopeI18nService,
  HisHopeLanguageSwitcherComponent,
  HisHopeOfflineBannerComponent,
  HisHopeThemeService,
  HisHopeToastComponent,
  HisHopeWorkspaceHeaderComponent,
} from '@his-hope/frontend-foundation';

@Component({
    selector: 'app-root',
    standalone: true,
    imports: [
        CommonModule, RouterModule,
        MatButtonModule, MatIconModule,
        MatSidenavModule,
        SidebarComponent, ErrorBarComponent, HisHopeLanguageSwitcherComponent, HisHopeOfflineBannerComponent, HisHopeWorkspaceHeaderComponent, HisHopeToastComponent,
    ],
    changeDetection: ChangeDetectionStrategy.OnPush,
    template: `
    <mat-sidenav-container class="app-sidenav-container">
      <hh-offline-banner></hh-offline-banner>
      @if (isLoggedIn) {
      <mat-sidenav #sidenav [mode]="sidenavMode" [opened]="isLoggedIn && sidenavOpened">
        <app-sidebar [sidenavOpened]="sidenavOpened"
                     (toggle)="toggleSidenav()"></app-sidebar>
      </mat-sidenav>
      }
      <mat-sidenav-content>
        @if (isLoggedIn && isMobile && !sidenavOpened) {
        <button mat-icon-button class="mobile-menu-button" (click)="openSidenav()" aria-label="Mở menu điều hướng">
          <mat-icon aria-hidden="true">menu</mat-icon>
        </button>
        }
        <app-error-bar></app-error-bar>
        @if (isLoggedIn) {
        <hh-workspace-header />
        }
        <div class="language-switcher"><hh-language-switcher /></div>
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
          message: `Đã chuyển từ ${this.previousUrl} đến ${event.url}`,
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
