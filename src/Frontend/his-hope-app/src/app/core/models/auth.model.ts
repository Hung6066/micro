export interface LoginRequest {
  username: string;
  password: string;
  deviceInfo?: string;
  ipAddress?: string;
  userAgent?: string;
}

export interface RegisterRequest {
  username: string;
  email: string;
  password: string;
  firstName: string;
  lastName: string;
  middleName?: string;
  licenseNumber?: string;
  specialty?: string;
}

export interface TokenResponse {
  accessToken: string;
  refreshToken: string;
  expiresAt: string;
  user: User;
}

export interface User {
  id: string;
  username: string;
  email: string;
  firstName: string;
  lastName: string;
  middleName?: string;
  fullName: string;
  licenseNumber?: string;
  specialty?: string;
  roles: string[];
  permissions?: string[];
}
