import { Injectable } from '@angular/core';

const SKIP_BFF_PREFIXES = ['/api/v1/auth/', '/api/v1/admin/', '/api/v1/errors', '/api/v1/audit/'];

@Injectable({ providedIn: 'root' })
export class BffRouterService {
  shouldUseBff(url: string): boolean {
    return !SKIP_BFF_PREFIXES.some(p => url.startsWith(p));
  }

  getFetchOptions(url: string, method: string): RequestInit & { headers?: Record<string, string> } {
    const headers: Record<string, string> = {};
    if (['POST', 'PUT', 'PATCH', 'DELETE'].includes(method)) {
      const csrfToken = this.getCookie('hishop_csrf');
      if (csrfToken) headers['X-CSRF-Token'] = csrfToken;
    }
    return { credentials: 'include', headers };
  }

  getCookie(name: string): string | null {
    const match = document.cookie.match(new RegExp(`(?:^|; )${name}=([^;]*)`));
    return match ? decodeURIComponent(match[1]) : null;
  }
}
