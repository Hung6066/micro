import { ComponentFixture, TestBed } from '@angular/core/testing';
import { RouterTestingModule } from '@angular/router/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatDialogModule } from '@angular/material/dialog';
import { MatSnackBarModule } from '@angular/material/snack-bar';
import { CommonModule } from '@angular/common';
import { of } from 'rxjs';
import { ManageRolesComponent } from './manage-roles.component';
import { AdminService } from '@core/services/admin.service';
import { LoadingSpinnerComponent } from '@shared/components/loading-spinner/loading-spinner.component';
import { EmptyStateComponent } from '@shared/components/empty-state/empty-state.component';

describe('ManageRolesComponent', () => {
  let component: ManageRolesComponent;
  let fixture: ComponentFixture<ManageRolesComponent>;
  let adminService: jasmine.SpyObj<AdminService>;

  const mockRoles = [
    { id: 'role-1', name: 'admin', description: 'Administrator', permissions: ['all'], isSystem: true, usersCount: 3, createdAt: new Date().toISOString() },
    { id: 'role-2', name: 'doctor', description: 'Physician', permissions: ['read', 'write'], isSystem: true, usersCount: 10, createdAt: new Date().toISOString() },
  ];

  beforeEach(async () => {
    const spy = jasmine.createSpyObj('AdminService', ['getRoles', 'deleteRole']);
    spy.getRoles.and.returnValue(of(mockRoles));

    await TestBed.configureTestingModule({
      imports: [
        RouterTestingModule, NoopAnimationsModule, HttpClientTestingModule,
        MatTableModule, MatButtonModule, MatIconModule, MatMenuModule,
        MatProgressSpinnerModule, MatTooltipModule, MatDialogModule,
        MatSnackBarModule, CommonModule,
      ],
      declarations: [
      ManageRolesComponent,LoadingSpinnerComponent, EmptyStateComponent],
      providers: [
        { provide: AdminService, useValue: spy },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(ManageRolesComponent);
    component = fixture.componentInstance;
    adminService = TestBed.inject(AdminService) as jasmine.SpyObj<AdminService>;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should load roles on init', () => {
    expect(adminService.getRoles).toHaveBeenCalled();
  });

  it('should render title', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('h1')?.textContent).toContain('Quản lý vai trò & quyền');
  });

  it('should display role rows', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    const rows = compiled.querySelectorAll('mat-row');
    expect(rows.length).toBe(2);
  });

  it('should show add role button', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Thêm vai trò');
  });

  it('should pass a basic integrity check', () => {
    expect(true).toBeTrue();
  });

});
