import {
  Directive,
  EmbeddedViewRef,
  TemplateRef,
  ViewContainerRef,
  computed,
  effect,
  inject,
  input,
} from '@angular/core';
import { HisHopePermissionService } from './his-hope-permission.service';

function normalize(value: string | readonly string[]): string[] {
  return Array.isArray(value) ? [...value] : value ? [value as string] : [];
}

/**
 * Structural directive gating template content on permission checks against
 * `HisHopePermissionService`. Reactive to permission snapshot changes (e.g.
 * loaded after login), unlike a one-shot `*ngIf`.
 *
 * Usage: `<button *hhHasPermission="'admin.users.write'">Create</button>`
 * or `*hhHasPermission="['a','b']; mode: 'any'"`.
 */
@Directive({ selector: '[hhHasPermission]', standalone: true })
export class HisHopeHasPermissionDirective {
  private readonly permissionService = inject(HisHopePermissionService);
  private readonly templateRef = inject(TemplateRef<unknown>);
  private readonly viewContainerRef = inject(ViewContainerRef);
  private view: EmbeddedViewRef<unknown> | null = null;

  readonly hhHasPermission = input<string | readonly string[]>([]);
  readonly hhHasPermissionMode = input<'any' | 'all'>('all');

  private readonly canRender = computed(() => {
    const required = normalize(this.hhHasPermission());
    if (!required.length) return true;
    this.permissionService.permissions();
    return this.hhHasPermissionMode() === 'any'
      ? this.permissionService.hasAny(required)
      : this.permissionService.hasAll(required);
  });

  constructor() {
    effect(() => {
      const shouldRender = this.canRender();
      if (shouldRender && !this.view) {
        this.view = this.viewContainerRef.createEmbeddedView(this.templateRef);
      } else if (!shouldRender && this.view) {
        this.viewContainerRef.clear();
        this.view = null;
      }
    });
  }
}
