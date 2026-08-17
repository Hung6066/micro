import { mutationPermission } from './admin-mutation-permissions';

describe('mutationPermission', () => {
  it('maps every admin mutation surface to an explicit permission', () => {
    expect(mutationPermission('users', 'activate')).toBe('admin.users.write');
    expect(mutationPermission('roles', 'publish')).toBe('admin.roles.write');
    expect(mutationPermission('clients', 'delete')).toBe('admin.clients.write');
    expect(mutationPermission('breakglass', 'create')).toBe('admin.breakglass.write');
    expect(mutationPermission('sessions', 'revoke')).toBe('admin.sessions.revoke');
    expect(mutationPermission('credentials', 'reset')).toBe('admin.credentials.reset');
    expect(mutationPermission('settings', 'update')).toBe('admin.settings.write');
    expect(mutationPermission('provisioning', 'reconcile')).toBe('admin.provisioning.manage');
    expect(mutationPermission('security-signals', 'retry')).toBe('admin.security-signals.manage');
    expect(mutationPermission('mobile', 'revoke')).toBe('admin.users.write');
    expect(mutationPermission('database', 'backup')).toBe('admin.settings.write');
  });
});
