import { Component, OnInit, OnDestroy, Input, Output, EventEmitter, ChangeDetectionStrategy, ChangeDetectorRef } from '@angular/core';
import { Router } from '@angular/router';
import { FormControl } from '@angular/forms';
import { Subject, takeUntil, debounceTime, distinctUntilChanged } from 'rxjs';
import { AuthService } from '@core/services/auth.service';
import { PatientService } from '@core/services/patient.service';
import { User } from '@core/models/auth.model';
import { Patient } from '@core/models/patient.model';

@Component({
  selector: 'app-sidebar',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="sidebar-header">
      <div class="brand">
        <mat-icon class="logo-icon" aria-hidden="true">local_hospital</mat-icon>
        <span class="logo-text">His.Hope</span>
      </div>
      <button mat-icon-button class="hide-desktop close-btn" (click)="toggle.emit()"
        aria-label="Đóng menu điều hướng">
        <mat-icon>close</mat-icon>
      </button>
    </div>

    <!-- Patient Quick Search -->
    <div class="sidebar-search">
      <mat-form-field appearance="outline" class="search-field" subscriptSizing="dynamic">
        <mat-icon matPrefix>search</mat-icon>
        <input matInput [formControl]="patientSearchControl" [matAutocomplete]="patientAuto"
               placeholder="Tìm bệnh nhân...">
      </mat-form-field>
      <mat-autocomplete #patientAuto="matAutocomplete" [displayWith]="displayPatientName"
                        (optionSelected)="onPatientSelected($event)">
        <mat-option *ngFor="let p of searchResults" [value]="p">
          <div class="search-result-item">
            <span class="result-name">{{ p.fullName }}</span>
            <span class="result-meta">{{ p.genderName }} · {{ p.age }}t · {{ p.id | slice:0:8 }}</span>
          </div>
        </mat-option>
        <mat-option *ngIf="searchResults.length === 0 && (patientSearchControl.value?.length ?? 0) >= 2" disabled>
          <span class="no-results">Không tìm thấy bệnh nhân</span>
        </mat-option>
      </mat-autocomplete>
    </div>

    <mat-nav-list class="sidebar-nav" aria-label="Điều hướng chính">
      <a mat-list-item routerLink="/dashboard" routerLinkActive="active" #rla1="routerLinkActive">
        <mat-icon matListItemIcon aria-hidden="true">dashboard</mat-icon>
        <span matListItemTitle>Dashboard</span>
      </a>
      <a mat-list-item routerLink="/patients" routerLinkActive="active" #rla2="routerLinkActive">
        <mat-icon matListItemIcon aria-hidden="true">people</mat-icon>
        <span matListItemTitle>Bệnh nhân</span>
      </a>
      <a mat-list-item routerLink="/appointments" routerLinkActive="active" #rla3="routerLinkActive">
        <mat-icon matListItemIcon aria-hidden="true">calendar_today</mat-icon>
        <span matListItemTitle>Lịch hẹn</span>
      </a>
      <a mat-list-item routerLink="/clinical" routerLinkActive="active" #rla4="routerLinkActive">
        <mat-icon matListItemIcon aria-hidden="true">medical_services</mat-icon>
        <span matListItemTitle>Lâm sàng</span>
      </a>
      <a mat-list-item routerLink="/pharmacy" routerLinkActive="active" #rla5="routerLinkActive">
        <mat-icon matListItemIcon aria-hidden="true">medication</mat-icon>
        <span matListItemTitle>Dược phẩm</span>
      </a>
      <a mat-list-item routerLink="/lab" routerLinkActive="active" #rla6="routerLinkActive">
        <mat-icon matListItemIcon aria-hidden="true">biotech</mat-icon>
        <span matListItemTitle>Xét nghiệm</span>
      </a>
      <a mat-list-item routerLink="/billing" routerLinkActive="active" #rla7="routerLinkActive">
        <mat-icon matListItemIcon aria-hidden="true">receipt</mat-icon>
        <span matListItemTitle>Thanh toán</span>
      </a>
      <a mat-list-item routerLink="/admin" routerLinkActive="active" #rla8="routerLinkActive">
        <mat-icon matListItemIcon aria-hidden="true">settings</mat-icon>
        <span matListItemTitle>Quản trị</span>
      </a>
    </mat-nav-list>

    <div class="sidebar-footer" *ngIf="currentUser">
      <div class="user-info">
        <span class="user-name" id="user-name">{{ currentUser.fullName }}</span>
        <span class="user-specialty">{{ currentUser.specialty }}</span>
      </div>
      <button mat-icon-button (click)="logout()" [disabled]="loggingOut"
        aria-label="Đăng xuất" aria-describedby="user-name">
        <mat-icon aria-hidden="true">logout</mat-icon>
      </button>
    </div>
  `,
  styles: [`
    :host {
      display: flex;
      flex-direction: column;
      height: 100%;
      background: var(--surface-white, #FFFFFF);
      border-right: 1px solid var(--border-default, #EAEAEA);
    }

    /* ── Header ── */
    .sidebar-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: 16px 16px 12px;
      flex-shrink: 0;
    }

    .brand {
      display: flex;
      align-items: center;
      gap: 10px;
    }

    .logo-icon {
      font-size: 28px;
      width: 28px;
      height: 28px;
      color: var(--color-primary, #2F6B4A);
    }

    .logo-text {
      font-size: 18px;
      font-weight: 600;
      color: var(--text-primary, #1A1A1A);
      letter-spacing: -0.01em;
    }

    .close-btn {
      color: var(--text-secondary, #787774);
    }

    /* ── Search ── */
    .sidebar-search {
      padding: 0 12px 12px;
      flex-shrink: 0;
    }

    .sidebar-search .search-field {
      width: 100%;
      --mdc-outlined-text-field-container-shape: 6px;
    }

    ::ng-deep .sidebar-search .mat-mdc-form-field-subscript-wrapper {
      display: none;
    }

    .search-result-item {
      display: flex;
      flex-direction: column;
      padding: 4px 0;
    }

    .result-name {
      font-weight: 500;
      font-size: 13px;
    }

    .result-meta {
      font-size: 11px;
      color: var(--text-secondary, #787774);
    }

    .no-results {
      color: var(--text-muted, #A1A09B);
      font-style: italic;
    }

    /* ── Navigation ── */
    .sidebar-nav {
      flex: 1;
      padding: 4px 8px;
      overflow-y: auto;
    }

    .sidebar-nav a {
      border-radius: 6px;
      margin-bottom: 2px;
      color: var(--text-primary, #1A1A1A);
      height: 44px;
      position: relative;
      transition: background-color 150ms ease;
    }

    .sidebar-nav a:hover {
      background: rgba(0, 0, 0, 0.035);
    }

    .sidebar-nav a.active {
      background: rgba(47, 107, 74, 0.06);
      color: var(--color-primary, #2F6B4A);
    }

    .sidebar-nav a.active::before {
      content: '';
      position: absolute;
      left: -8px;
      top: 8px;
      bottom: 8px;
      width: 3px;
      background: var(--color-primary, #2F6B4A);
      border-radius: 0 3px 3px 0;
    }

    .sidebar-nav ::ng-deep .mat-icon {
      color: var(--text-secondary, #787774);
      font-size: 20px;
      width: 20px;
      height: 20px;
    }

    .sidebar-nav a.active ::ng-deep .mat-icon {
      color: var(--color-primary, #2F6B4A);
    }

    ::ng-deep .sidebar-nav .mdc-list-item__primary-text {
      font-size: 14px;
      font-weight: 500;
      letter-spacing: 0.01em;
    }

    /* ── Footer ── */
    .sidebar-footer {
      border-top: 1px solid var(--border-light, #F0F0EE);
      padding: 12px 16px;
      display: flex;
      align-items: center;
      justify-content: space-between;
      flex-shrink: 0;
    }

    .user-info {
      display: flex;
      flex-direction: column;
      min-width: 0;
    }

    .user-name {
      font-size: 13px;
      font-weight: 500;
      color: var(--text-primary, #1A1A1A);
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    .user-specialty {
      font-size: 11px;
      color: var(--text-secondary, #787774);
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    .sidebar-footer button {
      color: var(--text-secondary, #787774);
      flex-shrink: 0;
    }

    .sidebar-footer button:hover {
      color: var(--color-warn, #C25450);
    }
  `],
})
export class SidebarComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();
  currentUser: User | null = null;
  loggingOut = false;

  // Patient search
  patientSearchControl = new FormControl('');
  searchResults: Patient[] = [];

  @Input() sidenavOpened = true;
  @Output() toggle = new EventEmitter<void>();

  constructor(
    private authService: AuthService,
    private patientService: PatientService,
    private router: Router,
    private cdr: ChangeDetectorRef,
  ) {}

  ngOnInit(): void {
    this.authService.currentUser$
      .pipe(takeUntil(this.destroy$))
      .subscribe((user) => {
        this.currentUser = user;
        this.cdr.markForCheck();
      });

    this.patientSearchControl.valueChanges
      .pipe(
        debounceTime(300),
        distinctUntilChanged(),
        takeUntil(this.destroy$),
      )
      .subscribe((term) => {
        const query = (term ?? '').trim();
        if (query.length < 2) {
          this.searchResults = [];
          this.cdr.markForCheck();
          return;
        }
        this.patientService.search(query, 1, 10)
          .pipe(takeUntil(this.destroy$))
          .subscribe((res) => {
            this.searchResults = res.items;
            this.cdr.markForCheck();
          });
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  displayPatientName(patient: Patient): string {
    return patient ? patient.fullName : '';
  }

  onPatientSelected(event: any): void {
    const patient: Patient = event.option.value;
    if (patient) {
      this.patientSearchControl.setValue('', { emitEvent: false });
      this.searchResults = [];
      this.router.navigate(['/patients', patient.id, 'workspace']);
      this.cdr.markForCheck();
    }
  }

  logout(): void {
    this.loggingOut = true;
    this.authService.logout()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        complete: () => {
          this.loggingOut = false;
          this.router.navigate(['/auth/login']);
          this.cdr.markForCheck();
        },
        error: () => {
          this.loggingOut = false;
          this.router.navigate(['/auth/login']);
          this.cdr.markForCheck();
        },
      });
  }
}
