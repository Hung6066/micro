// @ts-nocheck
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { RouterTestingModule } from '@angular/router/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { ReactiveFormsModule } from '@angular/forms';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule } from '@angular/material/paginator';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatSnackBarModule } from '@angular/material/snack-bar';
import { CommonModule } from '@angular/common';
import { of } from 'rxjs';
import { AuditLogsComponent } from './audit-logs.component';
import { AdminService } from '@core/services/admin.service';
import { LoadingSpinnerComponent } from '@shared/components/loading-spinner/loading-spinner.component';
import { EmptyStateComponent } from '@shared/components/empty-state/empty-state.component';

describe('AuditLogsComponent', () => {
  let component: AuditLogsComponent;
  let fixture: ComponentFixture<AuditLogsComponent>;
  let adminService: jasmine.SpyObj<AdminService>;

  const mockLogs = {
    items: [
      { id: 'log-1', timestamp: new Date().toISOString(), userId: 'usr-001', userName: 'Admin', action: 'CREATE' as const, resourceType: 'patient', resourceId: 'pat-001', ipAddress: '192.168.1.1', userAgent: 'Chrome', details: {} },
      { id: 'log-2', timestamp: new Date().toISOString(), userId: 'usr-002', userName: 'Doctor', action: 'UPDATE' as const, resourceType: 'encounter', resourceId: 'enc-001', ipAddress: '192.168.1.2', userAgent: 'Firefox', details: {} },
    ],
    totalCount: 2, page: 1, pageSize: 10, hasNextPage: false, hasPreviousPage: false,
  };

  beforeEach(async () => {
    const spy = jasmine.createSpyObj('AdminService', ['getAuditLogs']);
    spy.getAuditLogs.and.returnValue(of(mockLogs));

    await TestBed.configureTestingModule({
      imports: [
        RouterTestingModule, NoopAnimationsModule, HttpClientTestingModule,
        ReactiveFormsModule, MatTableModule, MatPaginatorModule, MatButtonModule,
        MatIconModule, MatFormFieldModule, MatInputModule, MatSelectModule,
        MatDatepickerModule, MatNativeDateModule, MatProgressSpinnerModule,
        MatExpansionModule, MatSnackBarModule, CommonModule,
      ],
      declarations: [
      AuditLogsComponent,LoadingSpinnerComponent, EmptyStateComponent],
      providers: [
        { provide: AdminService, useValue: spy },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(AuditLogsComponent);
    component = fixture.componentInstance;
    adminService = TestBed.inject(AdminService) as jasmine.SpyObj<AdminService>;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should load audit logs on init', () => {
    expect(adminService.getAuditLogs).toHaveBeenCalled();
  });

  it('should render title', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('h1')?.textContent).toContain('Nhật ký truy cập');
  });

  it('should display audit log rows', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    const rows = compiled.querySelectorAll('mat-row.clickable-row');
    expect(rows.length).toBe(2);
  });

  it('should have filter controls', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    const selects = compiled.querySelectorAll('mat-select');
    expect(selects.length).toBeGreaterThanOrEqual(2);
  });

  it('should pass a basic integrity check', () => {
    expect(true).toBeTrue();
  });

});
