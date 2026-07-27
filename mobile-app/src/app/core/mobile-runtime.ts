import { Capacitor } from '@capacitor/core';

export function resolveMobileApiOrigin(): string {
  if (Capacitor.isNativePlatform()) {
    if (Capacitor.getPlatform() === 'android') return 'http://10.0.2.2:5000';
    if (Capacitor.getPlatform() === 'ios') return 'http://localhost:5000';
  }

  if (typeof window !== 'undefined' && window.location.hostname && window.location.hostname !== 'localhost') {
    return `http://${window.location.hostname}:5000`;
  }

  return 'http://localhost:5000';
}

export function resolveMobileRedirectUri(path: '/auth/callback' | '/auth/logout-callback'): string {
  if (Capacitor.isNativePlatform()) {
    return path === '/auth/callback' ? 'hishope://auth/callback' : 'hishope://auth/logout-callback';
  }

  const origin = typeof window !== 'undefined' && window.location.origin
    ? window.location.origin
    : 'http://localhost:4300';
  return `${origin}${path}`;
}
