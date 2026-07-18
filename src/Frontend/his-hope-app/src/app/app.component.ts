import { Component, OnInit, OnDestroy, ChangeDetectionStrategy, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MatSidenavModule } from '@angular/material/sidenav';
import { Subject, takeUntil } from 'rxjs';
import { AuthService } from '@core/services/auth.service';
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
      <mat-sidenav #sidenav mode="side" [opened]="isLoggedIn && sidenavOpened"
                   *ngIf="isLoggedIn">
        <app-sidebar [sidenavOpened]="sidenavOpened"
                     (toggle)="toggleSidenav()"></app-sidebar>
      </mat-sidenav>
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

  constructor(
    private authService: AuthService,
    private cdr: ChangeDetectorRef,
    private rum: RumService,
  ) {}

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
        this.cdr.markForCheck();
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
