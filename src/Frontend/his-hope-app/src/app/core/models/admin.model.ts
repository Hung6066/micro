export interface AdminUser {
  id: string;
  username: string;
  email: string;
  fullName: string;
  phone: string;
  roles: string[];
  isActive: boolean;
  createdAt: string;
  updatedAt?: string;
  lastLoginAt?: string;
}

export interface Role {
  id: string;
  name: string;
  description: string;
  permissions: string[];
  isSystem: boolean;
  usersCount: number;
  createdAt: string;
}

export interface Permission {
  code: string;
  name: string;
  group: string;
  description: string;
}

export interface PermissionGroup {
  group: string;
  groupName: string;
  permissions: Permission[];
}

export interface Setting {
  key: string;
  value: any;
  type: 'text' | 'number' | 'boolean' | 'select';
  label: string;
  category: string;
  options?: { label: string; value: any }[];
}

export interface AuditLog {
  id: string;
  timestamp: string;
  userId: string;
  userName: string;
  action: 'CREATE' | 'READ' | 'UPDATE' | 'DELETE';
  resourceType: string;
  resourceId: string;
  ipAddress: string;
  userAgent: string;
  details: any;
}

export interface AdminDashboardStats {
  totalUsers: number;
  activeRoles: number;
  lastAuditEntry: string | null;
  systemHealth: 'healthy' | 'degraded' | 'down';
}
