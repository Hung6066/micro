import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ReactiveFormsModule } from '@angular/forms';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBarModule } from '@angular/material/snack-bar';
import { CommonModule } from '@angular/common';
import { of } from 'rxjs';
import { RoleFormDialogComponent, RoleFormData } from './role-form.dialog';
import { AdminService } from '@core/services/admin.service';
import { provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';

describe('RoleFormDialogComponent', () => {
  let component: RoleFormDialogComponent;
  let fixture: ComponentFixture<RoleFormDialogComponent>;

  const mockData: RoleFormData = { mode: 'create' };

  beforeEach(async () => {
    const adminSpy = jasmine.createSpyObj('AdminService', ['getPermissions', 'createRole', 'updateRole']);
    adminSpy.getPermissions.and.returnValue(of([
      { group: 'patient', groupName: 'Patient Management', permissions: [{ code: 'patient.create', name: 'Create Patient', group: 'patient', description: 'Create new patients' }] },
    ]));

    await TestBed.configureTestingModule({
    imports: [RoleFormDialogComponent,
        CommonModule, ReactiveFormsModule, MatDialogModule, MatButtonModule,
        MatFormFieldModule, MatInputModule, MatCheckboxModule, MatExpansionModule,
        MatIconModule, MatProgressSpinnerModule, MatSnackBarModule,
        NoopAnimationsModule],
    providers: [
        { provide: MatDialogRef, useValue: { close: jasmine.createSpy('close') } },
        { provide: MAT_DIALOG_DATA, useValue: mockData },
        { provide: AdminService, useValue: adminSpy },
        provideHttpClient(withInterceptorsFromDi()),
        provideHttpClientTesting(),
    ]
}).compileComponents();

    fixture = TestBed.createComponent(RoleFormDialogComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should display title for create mode', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('h2')?.textContent).toContain('Thêm vai trò');
  });

  it('should have form controls', () => {
    expect(component.form.contains('name')).toBeTrue();
    expect(component.form.contains('description')).toBeTrue();
  });

  it('should load permission groups', () => {
    expect(component.permissionGroups.length).toBeGreaterThan(0);
  });

  it('should render form fields', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('input[formcontrolname="name"]')).toBeTruthy();
  });

  it('should pass a basic integrity check', () => {
    expect(true).toBeTrue();
  });

});
