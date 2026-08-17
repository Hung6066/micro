import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { of } from 'rxjs';
import { HisHopePermissionService } from '@his-hope/frontend-foundation';
import { AdminApiService } from '../services/admin-api.service';
import { capabilityPermissionGuard } from './capability-permission.guard';

describe('capabilityPermissionGuard', () => {
  it('allows a capability route when the foundation snapshot has the read permission', (done) => {
    const permissions = { has: (permission: string) => permission === 'admin.settings.read', setSnapshot: jasmine.createSpy() };
    const router = { createUrlTree: jasmine.createSpy('createUrlTree') };
    TestBed.configureTestingModule({
      providers: [
        { provide: HisHopePermissionService, useValue: permissions },
        { provide: AdminApiService, useValue: { getMyPermissions: () => of({ roles: [], permissions: [] }) } },
        { provide: Router, useValue: router },
      ],
    });

    TestBed.runInInjectionContext(() => capabilityPermissionGuard()).subscribe(result => {
      expect(result).toBeTrue();
      expect(router.createUrlTree).not.toHaveBeenCalled();
      done();
    });
  });

  it('returns the foundation forbidden state when the server snapshot lacks the read permission', (done) => {
    const permissions = { has: () => false, setSnapshot: jasmine.createSpy() };
    const forbidden = { forbidden: true };
    const router = { createUrlTree: jasmine.createSpy('createUrlTree').and.returnValue(forbidden) };
    TestBed.configureTestingModule({
      providers: [
        { provide: HisHopePermissionService, useValue: permissions },
        { provide: AdminApiService, useValue: { getMyPermissions: () => of({ roles: [], permissions: [] }) } },
        { provide: Router, useValue: router },
      ],
    });

    TestBed.runInInjectionContext(() => capabilityPermissionGuard()).subscribe(result => {
      expect(result as unknown).toEqual(forbidden);
      expect(permissions.setSnapshot).toHaveBeenCalled();
      done();
    });
  });
});
