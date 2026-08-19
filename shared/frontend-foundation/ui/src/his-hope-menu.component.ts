import { ConnectedPosition, Overlay, OverlayRef } from '@angular/cdk/overlay';
import { TemplatePortal } from '@angular/cdk/portal';
import {
  ChangeDetectionStrategy,
  Component,
  Directive,
  ElementRef,
  EventEmitter,
  OnDestroy,
  Output,
  TemplateRef,
  ViewChild,
  ViewContainerRef,
  inject,
  input,
  signal,
} from '@angular/core';

const MENU_ITEM_SELECTOR = '[hh-menu-item]:not([disabled])';

/** Panel content host for `[hhMenuTriggerFor]`. Content only renders while open. */
@Component({
  selector: 'hh-menu',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ng-template #templateRef>
      <div class="hh-menu" role="menu" [attr.aria-label]="label()" (keydown)="onKeydown($event)">
        <ng-content />
      </div>
    </ng-template>
  `,
  styles: [
    `
      .hh-menu {
        display: flex;
        flex-direction: column;
        min-width: 180px;
        padding: 6px;
        border: 1px solid var(--border-default);
        border-radius: var(--radius-card);
        background: var(--surface-white);
        box-shadow: var(--shadow-dropdown);
      }
    `,
  ],
})
export class HisHopeMenuComponent {
  readonly label = input('Menu');
  @ViewChild('templateRef', { static: true }) readonly templateRef!: TemplateRef<unknown>;
  @Output() readonly closed = new EventEmitter<void>();

  onKeydown(event: KeyboardEvent): void {
    if (event.key === 'Escape') {
      event.preventDefault();
      this.closed.emit();
      return;
    }
    if (event.key !== 'ArrowDown' && event.key !== 'ArrowUp') return;
    const items = Array.from(
      (event.currentTarget as HTMLElement).querySelectorAll<HTMLElement>(MENU_ITEM_SELECTOR),
    );
    if (!items.length) return;
    event.preventDefault();
    const activeIndex = items.indexOf(document.activeElement as HTMLElement);
    const step = event.key === 'ArrowDown' ? 1 : -1;
    items[(activeIndex + step + items.length) % items.length]?.focus();
  }
}

/** Standardizes focusable menu item semantics; apply alongside `<button>`. */
@Directive({
  selector: '[hh-menu-item]',
  standalone: true,
  host: { class: 'hh-menu-item', role: 'menuitem', tabindex: '-1' },
})
export class HisHopeMenuItemDirective {}

const MENU_POSITIONS: ConnectedPosition[] = [
  { originX: 'start', originY: 'bottom', overlayX: 'start', overlayY: 'top', offsetY: 4 },
  { originX: 'start', originY: 'top', overlayX: 'start', overlayY: 'bottom', offsetY: -4 },
  { originX: 'end', originY: 'bottom', overlayX: 'end', overlayY: 'top', offsetY: 4 },
];

/** Opens `hh-menu` content anchored to the host element via CDK overlay. */
@Directive({
  selector: '[hhMenuTriggerFor]',
  standalone: true,
  host: {
    '(click)': 'toggle()',
    '[attr.aria-haspopup]': "'menu'",
    '[attr.aria-expanded]': 'isOpen()',
  },
})
export class HisHopeMenuTriggerDirective implements OnDestroy {
  readonly hhMenuTriggerFor = input.required<HisHopeMenuComponent>();
  private readonly overlay = inject(Overlay);
  private readonly elementRef = inject(ElementRef<HTMLElement>);
  private readonly viewContainerRef = inject(ViewContainerRef);
  private overlayRef: OverlayRef | null = null;
  private readonly isOpenSignal = signal(false);
  readonly isOpen = this.isOpenSignal.asReadonly();

  toggle(): void {
    if (this.isOpenSignal()) this.close();
    else this.open();
  }

  open(): void {
    if (this.overlayRef) return;
    const menu = this.hhMenuTriggerFor();
    const positionStrategy = this.overlay
      .position()
      .flexibleConnectedTo(this.elementRef)
      .withPositions(MENU_POSITIONS)
      .withFlexibleDimensions(false)
      .withPush(true);
    this.overlayRef = this.overlay.create({
      positionStrategy,
      hasBackdrop: true,
      backdropClass: 'cdk-overlay-transparent-backdrop',
      scrollStrategy: this.overlay.scrollStrategies.reposition(),
    });
    this.overlayRef.attach(new TemplatePortal(menu.templateRef, this.viewContainerRef));
    this.overlayRef.backdropClick().subscribe(() => this.close());
    this.overlayRef.keydownEvents().subscribe((event) => {
      if (event.key === 'Escape') this.close();
    });
    this.isOpenSignal.set(true);
    queueMicrotask(() =>
      this.overlayRef?.overlayElement.querySelector<HTMLElement>(MENU_ITEM_SELECTOR)?.focus(),
    );
  }

  close(): void {
    this.overlayRef?.dispose();
    this.overlayRef = null;
    this.isOpenSignal.set(false);
    this.elementRef.nativeElement.focus();
  }

  ngOnDestroy(): void {
    this.overlayRef?.dispose();
  }
}
