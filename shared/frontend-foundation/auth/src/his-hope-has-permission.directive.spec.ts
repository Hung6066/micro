import { Component, ViewChild } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HisHopeHasPermissionDirective } from './his-hope-has-permission.directive';
import { HisHopePermissionService } from './his-hope-permission.service';

@Component({
  standalone: true,
  imports: [HisHopeHasPermissionDirective],
  template: `
    <button *hhHasPermission="'admin.users.write'">Write</button>
    <button *hhHasPermission="['a', 'b']; mode: 'any'">Any</button>
  `,
})
class HostComponent {}

describe('HisHopeHasPermissionDirective', () => {
  let fixture: ComponentFixture<HostComponent>;
  let permissions: HisHopePermissionService;

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [HostComponent] });
    fixture = TestBed.createComponent(HostComponent);
    permissions = TestBed.inject(HisHopePermissionService);
  });

  it('hides content when the permission is missing', () => {
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).not.toContain('Write');
  });

  it('renders content once the required permission is granted', () => {
    permissions.setPermissions(['admin.users.write']);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Write');
  });

  it('reacts to permission snapshot changes after initial render', () => {
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).not.toContain('Write');
    permissions.setPermissions(['admin.users.write']);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Write');
  });

  it('supports mode="any" across multiple permissions', () => {
    permissions.setPermissions(['b']);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Any');
  });
});
