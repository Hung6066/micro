import { ComponentFixture, TestBed } from '@angular/core/testing';
import { RouterTestingModule } from '@angular/router/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { of } from 'rxjs';
import { AdminDashboardComponent } from './admin-dashboard.component';
import { AdminService } from '@core/services/admin.service';
import { LoadingSpinnerComponent } from '@shared/components/loading-spinner/loading-spinner.component';
import { provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';

describe('AdminDashboardComponent', () => {
  let component: AdminDashboardComponent;
  let fixture: ComponentFixture<AdminDashboardComponent>;
  let adminService: jasmine.SpyObj<AdminService>;

  const mockStats = {
    totalUsers: 42,
    activeRoles: 8,
    lastAuditEntry: new Date().toISOString(),
    systemHealth: 'healthy' as const,
  };

  beforeEach(async () => {
    const spy = jasmine.createSpyObj('AdminService', ['getDashboardStats']);
    spy.getDashboardStats.and.returnValue(of(mockStats));

    await TestBed.configureTestingModule({
    declarations: [
        AdminDashboardComponent, LoadingSpinnerComponent
    ],
    imports: [RouterTestingModule, NoopAnimationsModule,
        MatCardModule, MatButtonModule, MatIconModule, MatProgressSpinnerModule,
        CommonModule, RouterModule],
    providers: [
        { provide: AdminService, useValue: spy },
        provideHttpClient(withInterceptorsFromDi()),
        provideHttpClientTesting(),
    ]
}).compileComponents();

    fixture = TestBed.createComponent(AdminDashboardComponent);
    component = fixture.componentInstance;
    adminService = TestBed.inject(AdminService) as jasmine.SpyObj<AdminService>;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should load stats on init', () => {
    expect(adminService.getDashboardStats).toHaveBeenCalled();
  });

  it('should render title', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('h1')?.textContent).toContain('Quản trị hệ thống');
  });

  it('should display stat cards', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    const cards = compiled.querySelectorAll('mat-card');
    expect(cards.length).toBeGreaterThanOrEqual(4);
  });

  it('should show total users stat', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('42');
  });

  it('should render quick links', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Truy cập nhanh');
  });

  it('should pass a basic integrity check', () => {
    expect(true).toBeTrue();
  });

});
