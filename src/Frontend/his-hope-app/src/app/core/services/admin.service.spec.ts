import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AdminService } from './admin.service';
import { environment } from '@env/environment';
import { provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';

describe('AdminService', () => {
  let service: AdminService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
    imports: [],
    providers: [provideHttpClient(withInterceptorsFromDi()), provideHttpClientTesting()]
});
    service = TestBed.inject(AdminService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should get users with pagination', () => {
    const mockResult = {
      items: [{ id: 'usr-001', username: 'admin' }],
      totalCount: 1,
      page: 1,
      pageSize: 20,
      hasNextPage: false,
      hasPreviousPage: false,
    };

    service.getUsers({ page: 1, pageSize: 20 }).subscribe((result) => {
      expect(result.items.length).toBe(1);
    });

    const req = httpMock.expectOne((r) => r.url === `${environment.apiUrl}/admin/users`);
    expect(req.request.method).toBe('GET');
    req.flush(mockResult);
  });

  it('should get user by id', () => {
    service.getUser('usr-001').subscribe((user) => {
      expect(user.id).toBe('usr-001');
    });

    const req = httpMock.expectOne(`${environment.apiUrl}/admin/users/usr-001`);
    expect(req.request.method).toBe('GET');
    req.flush({ id: 'usr-001' });
  });

  it('should create user', () => {
    const data = { username: 'newuser', password: 'secret' };
    service.createUser(data as any).subscribe((user) => {
      expect(user.username).toBe('newuser');
    });

    const req = httpMock.expectOne(`${environment.apiUrl}/admin/users`);
    expect(req.request.method).toBe('POST');
    req.flush({ id: 'usr-002', ...data });
  });

  it('should get roles', () => {
    service.getRoles().subscribe((roles) => {
      expect(roles.length).toBe(2);
    });

    const req = httpMock.expectOne(`${environment.apiUrl}/admin/roles`);
    expect(req.request.method).toBe('GET');
    req.flush([{ id: 'role-001', name: 'Admin' }, { id: 'role-002', name: 'Doctor' }]);
  });

  it('should get permissions', () => {
    service.getPermissions().subscribe((groups) => {
      expect(groups.length).toBe(1);
    });

    const req = httpMock.expectOne(`${environment.apiUrl}/admin/permissions`);
    expect(req.request.method).toBe('GET');
    req.flush([{ group: 'patients', groupName: 'Patients', permissions: [] }]);
  });

  it('should deactivate user', () => {
    service.deactivateUser('usr-001').subscribe();

    const req = httpMock.expectOne(`${environment.apiUrl}/admin/users/usr-001/deactivate`);
    expect(req.request.method).toBe('POST');
    req.flush(null);
  });

  it('should activate user', () => {
    service.activateUser('usr-001').subscribe();

    const req = httpMock.expectOne(`${environment.apiUrl}/admin/users/usr-001/activate`);
    expect(req.request.method).toBe('POST');
    req.flush(null);
  });

  it('should assign roles', () => {
    service.assignRoles('usr-001', ['role-1', 'role-2']).subscribe((user) => {
      expect(user.roles).toContain('admin');
    });

    const req = httpMock.expectOne(`${environment.apiUrl}/admin/users/usr-001/roles`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ roleIds: ['role-1', 'role-2'] });
    req.flush({ id: 'usr-001', username: 'admin', roles: ['admin'] });
  });

  it('should get settings', () => {
    const mockSettings = [{ key: 'hospital_name', value: 'His.Hope', type: 'text', label: 'Hospital Name', category: 'hospital' }];
    service.getSettings().subscribe((settings) => {
      expect(settings.length).toBe(1);
    });
    const req = httpMock.expectOne(`${environment.apiUrl}/admin/settings`);
    expect(req.request.method).toBe('GET');
    req.flush(mockSettings);
  });

  it('should get audit logs with params', () => {
    const mockResult = { items: [{ id: 'log-001' }], totalCount: 1, page: 1, pageSize: 20, hasNextPage: false, hasPreviousPage: false };
    service.getAuditLogs({ page: 1, pageSize: 20 }).subscribe((result) => {
      expect(result.items.length).toBe(1);
    });
    const req = httpMock.expectOne(r => r.url.includes('/admin/audit-logs') && r.params.get('page') === '1');
    expect(req.request.method).toBe('GET');
    req.flush(mockResult);
  });

  it('should delete role', () => {
    service.deleteRole('role-1').subscribe();
    const req = httpMock.expectOne(`${environment.apiUrl}/admin/roles/role-1`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
  });

  it('should get role by id', () => {
    service.getRole('role-1').subscribe((role) => {
      expect(role.name).toBe('Admin');
    });
    const req = httpMock.expectOne(`${environment.apiUrl}/admin/roles/role-1`);
    expect(req.request.method).toBe('GET');
    req.flush({ id: 'role-1', name: 'Admin' });
  });

  it('should create role', () => {
    const data = { name: 'NewRole', description: 'New role', permissions: ['read'] };
    service.createRole(data).subscribe((role) => {
      expect(role.name).toBe('NewRole');
    });
    const req = httpMock.expectOne(`${environment.apiUrl}/admin/roles`);
    expect(req.request.method).toBe('POST');
    req.flush({ id: 'role-3', ...data });
  });

  it('should update role', () => {
    const data = { name: 'Updated', description: 'Updated desc', permissions: ['read', 'write'] };
    service.updateRole('role-1', data).subscribe((role) => {
      expect(role.name).toBe('Updated');
    });
    const req = httpMock.expectOne(`${environment.apiUrl}/admin/roles/role-1`);
    expect(req.request.method).toBe('PUT');
    req.flush({ id: 'role-1', ...data });
  });

  it('should update user', () => {
    service.updateUser('usr-001', { fullName: 'Updated' }).subscribe((user) => {
      expect(user.fullName).toBe('Updated');
    });
    const req = httpMock.expectOne(`${environment.apiUrl}/admin/users/usr-001`);
    expect(req.request.method).toBe('PUT');
    req.flush({ id: 'usr-001', fullName: 'Updated' });
  });

  it('should get dashboard stats', () => {
    service.getDashboardStats().subscribe((stats) => {
      expect(stats.totalUsers).toBe(10);
    });
    const req = httpMock.expectOne(`${environment.apiUrl}/admin/dashboard`);
    expect(req.request.method).toBe('GET');
    req.flush({ totalUsers: 10, activeRoles: 5, lastAuditEntry: new Date().toISOString(), systemHealth: 'healthy' });
  });

  it('should get setting by key', () => {
    service.getSetting('hospital_name').subscribe((setting) => {
      expect(setting.value).toBe('His.Hope');
    });
    const req = httpMock.expectOne(`${environment.apiUrl}/admin/settings/hospital_name`);
    expect(req.request.method).toBe('GET');
    req.flush({ key: 'hospital_name', value: 'His.Hope' });
  });

  it('should update setting', () => {
    service.updateSetting('hospital_name', 'New Name').subscribe();
    const req = httpMock.expectOne(`${environment.apiUrl}/admin/settings/hospital_name`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({ value: 'New Name' });
    req.flush(null);
  });

  it('should bulk update settings', () => {
    const data = [{ key: 'k1', value: 'v1' }];
    service.bulkUpdateSettings(data).subscribe();
    const req = httpMock.expectOne(`${environment.apiUrl}/admin/settings/bulk`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({ settings: data });
    req.flush(null);
  });

  it('should get audit log by id', () => {
    service.getAuditLog('log-001').subscribe((log) => {
      expect(log.action).toBe('CREATE');
    });
    const req = httpMock.expectOne(`${environment.apiUrl}/admin/audit-logs/log-001`);
    expect(req.request.method).toBe('GET');
    req.flush({ id: 'log-001', action: 'CREATE' });
  });
});
