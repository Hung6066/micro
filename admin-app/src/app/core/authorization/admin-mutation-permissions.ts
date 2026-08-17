export type AdminMutationSurface = 'users' | 'roles' | 'clients' | 'breakglass' | 'sessions' | 'credentials' | 'settings' | 'provisioning' | 'security-signals' | 'mobile' | 'database';

export function mutationPermission(surface: AdminMutationSurface, _action: string): string {
  switch (surface) {
    case 'users': return 'admin.users.write';
    case 'roles': return 'admin.roles.write';
    case 'clients': return 'admin.clients.write';
    case 'breakglass': return 'admin.breakglass.write';
    case 'sessions': return 'admin.sessions.revoke';
    case 'credentials': return 'admin.credentials.reset';
    case 'settings': return 'admin.settings.write';
    case 'provisioning': return 'admin.provisioning.manage';
    case 'security-signals': return 'admin.security-signals.manage';
    case 'mobile': return 'admin.users.write';
    case 'database': return 'admin.settings.write';
  }
}
