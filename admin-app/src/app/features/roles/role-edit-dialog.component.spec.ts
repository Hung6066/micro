import { TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { of } from 'rxjs';
import { AdminApiService, PermissionDefinition, Role } from '../../core/services/admin-api.service';
import { RoleEditDialogComponent } from './role-edit-dialog.component';

describe('RoleEditDialogComponent', () => {
  const catalog: PermissionDefinition[] = [
    { code: 'clinical.read', name: 'Read clinical', group: 'Clinical', isSystem: true },
    { code: 'clinical.sign', name: 'Sign clinical', group: 'Clinical', isSystem: true },
    { code: 'billing.view', name: 'View billing', group: 'Billing', isSystem: true },
  ];

  function configure(data: Role | null = null): jasmine.SpyObj<AdminApiService> {
    const api = jasmine.createSpyObj<AdminApiService>('AdminApiService', ['getPermissions', 'getRoleOwners', 'createRole', 'updateRole']);
    api.getPermissions.and.returnValue(of(catalog));
    api.getRoleOwners.and.returnValue(of([{ key: 'identity-service', name: 'identity-service' }]));
    api.createRole.and.returnValue(of({ id: 'new-role', name: 'Role' } as Role));
    api.updateRole.and.returnValue(of({ id: 'role-1', name: 'Role' } as Role));
    TestBed.configureTestingModule({
      imports: [RoleEditDialogComponent],
      providers: [
        { provide: AdminApiService, useValue: api },
        { provide: MAT_DIALOG_DATA, useValue: data },
        { provide: MatDialogRef, useValue: { close: jasmine.createSpy('close') } },
        { provide: MatSnackBar, useValue: { open: jasmine.createSpy('open') } },
      ],
    });
    return api;
  }

  it('loads catalog, groups permissions and preserves selected role permissions', () => {
    configure({ id: 'role-1', name: 'Provider', permissions: [{ ...catalog[0] }] });
    const fixture = TestBed.createComponent(RoleEditDialogComponent);
    fixture.detectChanges();

    const component = fixture.componentInstance;
    expect(component.permissionGroups.map(group => group.name)).toEqual(['Billing', 'Clinical']);
    expect(component.selectedPermissionCodes).toEqual(new Set(['clinical.read']));
    expect(component.isGroupPartiallySelected(component.permissionGroups[1])).toBeTrue();
  });

  it('sends normalized selected permission codes when creating a role', () => {
    const api = configure();
    const fixture = TestBed.createComponent(RoleEditDialogComponent);
    fixture.detectChanges();
    const component = fixture.componentInstance;
    component.form.name = 'Clinical reviewer';
    component.togglePermission('clinical.sign', true);
    component.togglePermission('clinical.read', true);
    component.save();

    expect(api.createRole).toHaveBeenCalledWith(jasmine.objectContaining({
      name: 'Clinical reviewer',
      permissions: ['clinical.read', 'clinical.sign'],
    }));
  });

  it('keeps permission groups expanded for editing and supports collapsing them', () => {
    configure();
    const fixture = TestBed.createComponent(RoleEditDialogComponent);
    fixture.detectChanges();
    const component = fixture.componentInstance;

    expect(component.isGroupExpanded('Clinical')).toBeTrue();
    component.collapseGroup('Clinical');
    expect(component.isGroupExpanded('Clinical')).toBeFalse();
    component.expandGroup('Clinical');
    expect(component.isGroupExpanded('Clinical')).toBeTrue();
  });
});
