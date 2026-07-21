import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';

const BFF_URL_PREFIX_FLAGS: Record<string, string> = {
  '/api/v1/patients': 'bff.patient.enabled',
  '/api/v1/encounters': 'bff.clinical.enabled',
  '/api/v1/lab-orders': 'bff.lab.enabled',
  '/api/v1/invoices': 'bff.billing.enabled',
  '/api/v1/medications': 'bff.pharmacy.enabled',
  '/api/v1/prescriptions': 'bff.pharmacy.enabled',
  '/api/v1/dashboard': 'bff.dashboard.enabled',
  '/api/v1/critical-alerts': 'bff.lab.enabled',
};

const SKIP_BFF_PREFIXES = ['/api/v1/auth/', '/api/v1/admin/', '/api/v1/errors', '/api/v1/audit/'];

@Injectable({ providedIn: 'root' })
export class BffRouterService {
  private accessToken: string | null = null;
  private cookieAuthOnly = false;

  constructor() {
    this.cookieAuthOnly = (window as any).__UNLEASH_FLAGS__?.['bff.auth.cookie-only'] ?? false;
    this.accessToken = this.readAccessToken();
  }

  setAccessToken(token: string | null): void {
    this.accessToken = token;
    this.writeAccessToken(token);
  }

  shouldUseBff(url: string): boolean {
    if (SKIP_BFF_PREFIXES.some(p => url.startsWith(p))) return false;
    if (this.cookieAuthOnly) return true;
    const flag = Object.entries(BFF_URL_PREFIX_FLAGS)
      .find(([prefix]) => url.startsWith(prefix));
    return flag ? ((window as any).__UNLEASH_FLAGS__?.[flag[1]] ?? false) : false;
  }

  getFetchOptions(url: string, method: string): RequestInit & { headers?: Record<string, string> } {
    if (this.shouldUseBff(url)) {
      const headers: Record<string, string> = {};
      if (['POST', 'PUT', 'PATCH', 'DELETE'].includes(method)) {
        const csrfToken = this.getCookie('hishop_csrf');
        if (csrfToken) headers['X-CSRF-Token'] = csrfToken;
      }
      return { credentials: 'include', headers };
    }
    return {
      headers: this.accessToken
        ? { 'Authorization': `Bearer ${this.accessToken}` }
        : {}
    };
  }

  getCookie(name: string): string | null {
    const match = document.cookie.match(new RegExp(`(?:^|; )${name}=([^;]*)`));
    return match ? decodeURIComponent(match[1]) : null;
  }

  private readAccessToken(): string | null {
    try {
      return sessionStorage.getItem('hishop_access_token');
    } catch {
      return null;
    }
  }

  private writeAccessToken(token: string | null): void {
    try {
      if (token) {
        sessionStorage.setItem('hishop_access_token', token);
      } else {
        sessionStorage.removeItem('hishop_access_token');
      }
    } catch {
    }
  }
}
