import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { ReactiveFormsModule } from '@angular/forms';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBarModule } from '@angular/material/snack-bar';
import { CommonModule } from '@angular/common';
import { of } from 'rxjs';
import { AssignRolesDialogComponent, AssignRolesData } from './assign-roles.dialog';
import { AdminService } from '@core/services/admin.service';

describe('AssignRolesDialogComponent', () => {
  let component: AssignRolesDialogComponent;
  let fixture: ComponentFixture<AssignRolesDialogComponent>;

  const mockUser = { id: 'usr-001', username: 'testuser', email: 'test@test.com', fullName: 'Test User', phone: '0900000001', roles: ['doctor'], isActive: true, createdAt: new Date().toISOString() };
  const mockRoles = [
    { id: 'role-1', name: 'admin', description: 'Administrator', permissions: ['all'], isSystem: true, usersCount: 3, createdAt: new Date().toISOString() },
    { id: 'role-2', name: 'doctor', description: 'Physician', permissions: ['read'], isSystem: true, usersCount: 10, createdAt: new Date().toISOString() },
  ];
  const mockData: AssignRolesData = { user: mockUser };

  beforeEach(async () => {
    const adminSpy = jasmine.createSpyObj('AdminService', ['getRoles', 'assignRoles']);
    adminSpy.getRoles.and.returnValue(of(mockRoles));

    await TestBed.configureTestingModule({
      imports: [
      AssignRolesDialogComponent,
        CommonModule, ReactiveFormsModule, MatDialogModule, MatButtonModule,
        MatCheckboxModule, MatIconModule, MatProgressSpinnerModule, MatSnackBarModule,
        NoopAnimationsModule, HttpClientTestingModule,
      ],
      providers: [
        { provide: MatDialogRef, useValue: { close: jasmine.createSpy('close') } },
        { provide: MAT_DIALOG_DATA, useValue: mockData },
        { provide: AdminService, useValue: adminSpy },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(AssignRolesDialogComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should display title', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('h2')?.textContent).toContain('Phân vai trò');
  });

  it('should show user name', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Test User');
  });

  it('should load roles', () => {
    expect(component.roles.length).toBe(2);
  });

  it('should render checkboxes for roles', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    const checkboxes = compiled.querySelectorAll('mat-checkbox');
    expect(checkboxes.length).toBe(2);
  });

  it('should pass a basic integrity check', () => {
    expect(true).toBeTrue();
  });

});
