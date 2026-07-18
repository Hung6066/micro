import { Component, OnInit, OnDestroy, ChangeDetectionStrategy, ChangeDetectorRef, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router, NavigationEnd } from '@angular/router';
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
        MatSidenavModule,
        SidebarComponent, ErrorBarComponent,
    ],
    changeDetection: ChangeDetectionStrategy.OnPush,
    template: `
    <mat-sidenav-container class="app-sidenav-container">
      @if (isLoggedIn) {
      <mat-sidenav #sidenav mode="side" [opened]="isLoggedIn && sidenavOpened">
        <app-sidebar [sidenavOpened]="sidenavOpened"
                     (toggle)="toggleSidenav()"></app-sidebar>
      </mat-sidenav>
      }
      <mat-sidenav-content>
        <app-error-bar></app-error-bar>
        <div class="main-content" id="main-content">
          <router-outlet></router-outlet>
        </div>
      </mat-sidenav-content>
    </mat-sidenav-container>
  `,
    styles: [`
    .app-sidenav-container { height: 100vh; background: var(--bg-warm, #F7F6F3); }
    .app-sidenav-container .mat-drawer-side { border-right: 1px solid var(--border-default, #EAEAEA); }
    .main-content { min-height: 100vh; padding: 0; }
    :host ::ng-deep .mat-drawer-inner-container { overflow-x: hidden; }
  `],
})
export class AppComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();
  isLoggedIn = false;
  sidenavOpened = true;

  private authService = inject(AuthService);
  private router = inject(Router);
  private auditService = inject(AuditService);
  private cdr = inject(ChangeDetectorRef);
  private rum = inject(RumService);

  private previousUrl = '';

  toggleSidenav(): void {
    this.sidenavOpened = !this.sidenavOpened;
  }

  ngOnInit(): void {
    // Initialise Real User Monitoring (Web Vitals + OpenTelemetry).
    this.rum.initialize();

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
    });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
