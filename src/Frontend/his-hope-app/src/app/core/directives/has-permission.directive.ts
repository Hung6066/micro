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
 * when the current user lacks the specified permission(s).
 *
 * Usage:
 *   <button *hasPermission="'patients.write'">Thêm bệnh nhân</button>
 *   <div *hasPermission="['patients.view', 'appointments.view']">...</div>
 *
 * - String input: user must have that single permission.
 * - Array input: user must have ALL permissions (AND logic).
 */
@Directive({
  selector: '[hasPermission]',
  standalone: true,
})
export class HasPermissionDirective implements OnInit, OnDestroy {
  private requiredPermissions: string[] = [];
  private destroy$ = new Subject<void>();

  @Input()
  set hasPermission(value: string | string[]) {
    this.requiredPermissions = typeof value === 'string' ? [value] : value;
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
    const hasPermission = this.authService.hasPermission(this.requiredPermissions);
    if (hasPermission) {
      this.viewContainerRef.createEmbeddedView(this.templateRef);
    } else {
      this.viewContainerRef.clear();
    }
  }
}
