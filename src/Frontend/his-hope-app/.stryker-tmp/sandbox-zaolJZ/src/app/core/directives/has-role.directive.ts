// @ts-nocheck
import {
  Directive,
  Input,
  TemplateRef,
  ViewContainerRef,
  OnInit,
  OnDestroy,
} from '@angular/core';
import { Subject } from 'rxjs';
import { takeUntil } from 'rxjs/operators';
import { AuthService } from '@core/services/auth.service';

/**
 * Structural directive that conditionally removes an element from the DOM
 * when the current user lacks any of the specified role(s).
 *
 * Usage:
 *   <div *hasRole="'Admin'">Quản trị</div>
 *   <div *hasRole="['Admin', 'Manager']">Quản lý</div>
 *
 * - String input: user must have that single role.
 * - Array input: user must have at least one of the roles (OR logic).
 */
@Directive({
  selector: '[hasRole]',
  standalone: true,
})
export class HasRoleDirective implements OnInit, OnDestroy {
  private requiredRoles: string[] = [];
  private destroy$ = new Subject<void>();

  @Input()
  set hasRole(value: string | string[]) {
    this.requiredRoles = typeof value === 'string' ? [value] : value;
  }

  constructor(
    private templateRef: TemplateRef<unknown>,
    private viewContainerRef: ViewContainerRef,
    private authService: AuthService,
  ) {}

  ngOnInit(): void {
    this.authService.currentUser$
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        this.updateView();
      });

    // Initial render
    this.updateView();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private updateView(): void {
    const hasRole = this.authService.hasRole(this.requiredRoles);
    if (hasRole) {
      this.viewContainerRef.createEmbeddedView(this.templateRef);
    } else {
      this.viewContainerRef.clear();
    }
  }
}
