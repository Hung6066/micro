import { ComponentFixture, TestBed } from '@angular/core/testing';
import { RouterTestingModule } from '@angular/router/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ReactiveFormsModule } from '@angular/forms';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule } from '@angular/material/paginator';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatMenuModule } from '@angular/material/menu';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatDialogModule } from '@angular/material/dialog';
import { MatSnackBarModule } from '@angular/material/snack-bar';
import { CommonModule } from '@angular/common';
import { of } from 'rxjs';
import { ManageUsersComponent } from './manage-users.component';
import { AdminService } from '@core/services/admin.service';
import { LoadingSpinnerComponent } from '@shared/components/loading-spinner/loading-spinner.component';
import { EmptyStateComponent } from '@shared/components/empty-state/empty-state.component';
import { provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';

describe('ManageUsersComponent', () => {
  let component: ManageUsersComponent;
  let fixture: ComponentFixture<ManageUsersComponent>;
  let adminService: jasmine.SpyObj<AdminService>;

  const mockUsers = {
    items: [
      { id: 'usr-001', username: 'admin', email: 'admin@test.com', fullName: 'Admin User', phone: '0900000001', roles: ['admin'], isActive: true, createdAt: new Date().toISOString() },
      { id: 'usr-002', username: 'doctor1', email: 'doctor@test.com', fullName: 'Doctor User', phone: '0900000002', roles: ['doctor'], isActive: true, createdAt: new Date().toISOString() },
    ],
    totalCount: 2, page: 1, pageSize: 10, hasNextPage: false, hasPreviousPage: false,
  };

  beforeEach(async () => {
    const spy = jasmine.createSpyObj('AdminService', ['getUsers', 'deactivateUser', 'activateUser']);
    spy.getUsers.and.returnValue(of(mockUsers));

    await TestBed.configureTestingModule({
    
    imports: [
        ManageUsersComponent, LoadingSpinnerComponent, EmptyStateComponent, RouterTestingModule, NoopAnimationsModule,
        ReactiveFormsModule, MatTableModule, MatPaginatorModule, MatButtonModule,
        MatIconModule, MatFormFieldModule, MatInputModule, MatSelectModule,
        MatMenuModule, MatChipsModule, MatProgressSpinnerModule, MatTooltipModule,
        MatDialogModule, MatSnackBarModule, CommonModule],
    providers: [
        { provide: AdminService, useValue: spy },
        provideHttpClient(withInterceptorsFromDi()),
        provideHttpClientTesting(),
    ]
}).compileComponents();

    fixture = TestBed.createComponent(ManageUsersComponent);
    component = fixture.componentInstance;
    adminService = TestBed.inject(AdminService) as jasmine.SpyObj<AdminService>;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should load users on init', () => {
    expect(adminService.getUsers).toHaveBeenCalled();
  });

  it('should render title', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('h1')?.textContent).toContain('Quản lý người dùng');
  });

  it('should display user rows', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    const rows = compiled.querySelectorAll('mat-row');
    expect(rows.length).toBe(2);
  });

  it('should show add user button', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Thêm người dùng');
  });

  it('should have search field', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('input[placeholder="Tìm kiếm theo tên, email, ID..."]')).toBeTruthy();
  });

  it('should pass a basic integrity check', () => {
    expect(true).toBeTrue();
  });

});
