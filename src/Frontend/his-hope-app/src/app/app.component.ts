import { Component, OnInit, OnDestroy, ChangeDetectionStrategy, ChangeDetectorRef, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router, NavigationEnd } from '@angular/router';
import { BreakpointObserver } from '@angular/cdk/layout';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSidenavModule } from '@angular/material/sidenav';
import { Subject, takeUntil, filter } from 'rxjs';
import { AuthService } from '@core/services/auth.service';
import { AuditService } from '@core/services/audit.service';
import { RumService } from './monitoring/rum.service';
import { SidebarComponent } from '@shared/components/sidebar/sidebar.component';
import { ErrorBarComponent } from '@shared/components/error-bar/error-bar.component';

@Component({
    selector: 'app-root',
    standalone: true,
    imports: [
        CommonModule, RouterModule,
        MatButtonModule, MatIconModule,
        MatSidenavModule,
        SidebarComponent, ErrorBarComponent,
    ],
    changeDetection: ChangeDetectionStrategy.OnPush,
    template: `
    <mat-sidenav-container class="app-sidenav-container">
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
        <div class="main-content" id="main-content">
          <router-outlet></router-outlet>
        </div>
      </mat-sidenav-content>
    </mat-sidenav-container>
  `,
    styles: [`
    .app-sidenav-container { min-height: 100dvh; background: var(--bg-warm, #F7F6F3); }
    .app-sidenav-container .mat-drawer-side { border-right: 1px solid var(--border-default, #EAEAEA); }
    .main-content { min-height: 100dvh; padding: 0; position: relative; }
    .mobile-menu-button {
      position: fixed;
      top: 12px;
      left: 12px;
      z-index: 5;
      background: var(--surface-white, #FFFFFF);
      border: 1px solid var(--border-default, #EAEAEA);
      border-radius: 6px;
      color: var(--text-primary, #1A1A1A);
    }
    :host ::ng-deep .mat-drawer-inner-container { overflow-x: hidden; }
  `],
})
export class AppComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();
  isLoggedIn = false;
  sidenavOpened = true;
  sidenavMode: 'side' | 'over' = 'side';
  isMobile = false;

  private authService = inject(AuthService);
  private router = inject(Router);
  private auditService = inject(AuditService);
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
        // Set user ID for audit events
        this.auditService.setUserId(user?.id);
        this.cdr.markForCheck();
      });

    // Navigation audit — log mỗi lần user đổi route
    this.router.events.pipe(
      filter((event): event is NavigationEnd => event instanceof NavigationEnd),
      takeUntil(this.destroy$),
    ).subscribe((event: NavigationEnd) => {
      if (this.previousUrl && this.previousUrl !== event.url) {
        this.auditService.log('navigation.change', {
          from: this.previousUrl,
          to: event.url,
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
