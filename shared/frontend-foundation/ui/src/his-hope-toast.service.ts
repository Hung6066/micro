import { Injectable, signal } from '@angular/core';

export type HisHopeToastTone = 'success' | 'info' | 'warning' | 'error';

export interface HisHopeToast {
  id: number;
  message: string;
  tone: HisHopeToastTone;
  detail?: string;
  duration: number;
}

@Injectable({ providedIn: 'root' })
export class HisHopeToastService {
  private nextId = 1;
  private readonly toastSignal = signal<HisHopeToast[]>([]);
  readonly toasts = this.toastSignal.asReadonly();

  show(message: string, tone: HisHopeToastTone = 'info', options: { detail?: string; duration?: number } = {}): number {
    const toast: HisHopeToast = {
      id: this.nextId++,
      message,
      tone,
      detail: options.detail,
      duration: options.duration ?? 5000,
    };
    this.toastSignal.update(toasts => [...toasts, toast]);
    if (toast.duration > 0) window.setTimeout(() => this.dismiss(toast.id), toast.duration);
    return toast.id;
  }

  success(message: string, options?: { detail?: string; duration?: number }): number {
    return this.show(message, 'success', options);
  }

  info(message: string, options?: { detail?: string; duration?: number }): number {
    return this.show(message, 'info', options);
  }

  warning(message: string, options?: { detail?: string; duration?: number }): number {
    return this.show(message, 'warning', options);
  }

  error(message: string, options?: { detail?: string; duration?: number }): number {
    return this.show(message, 'error', options);
  }

  dismiss(id: number): void {
    this.toastSignal.update(toasts => toasts.filter(toast => toast.id !== id));
  }
}
