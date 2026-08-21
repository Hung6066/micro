import { isPlatformBrowser } from '@angular/common';
import { ConnectedPosition, Overlay, OverlayRef } from '@angular/cdk/overlay';
import { ComponentPortal } from '@angular/cdk/portal';
import {
  ChangeDetectionStrategy,
  Component,
  Directive,
  ElementRef,
  Input,
  OnDestroy,
  PLATFORM_ID,
  inject,
} from '@angular/core';

@Component({
  selector: 'hh-tooltip-panel',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<div class="hh-tooltip" role="tooltip" [id]="tooltipId">{{ text }}</div>`,
  styles: [
    `
      .hh-tooltip {
        max-width: 240px;
        padding: var(--space-xs) var(--space-md);
        border-radius: var(--radius-control);
        background: var(--text-primary);
        color: var(--surface-white);
        font-size: var(--font-size-caption);
        line-height: var(--leading-tight);
        box-shadow: var(--shadow-dropdown);
      }
    `,
  ],
})
export class HisHopeTooltipPanelComponent {
  text = '';
  tooltipId = '';
}

type HisHopeTooltipPosition = 'above' | 'below' | 'start' | 'end';

const TOOLTIP_POSITIONS: Record<HisHopeTooltipPosition, ConnectedPosition> = {
  above: { originX: 'center', originY: 'top', overlayX: 'center', overlayY: 'bottom', offsetY: -8 },
  below: { originX: 'center', originY: 'bottom', overlayX: 'center', overlayY: 'top', offsetY: 8 },
  start: { originX: 'start', originY: 'center', overlayX: 'end', overlayY: 'center', offsetX: -8 },
  end: { originX: 'end', originY: 'center', overlayX: 'start', overlayY: 'center', offsetX: 8 },
};

/** Accessible hover/focus tooltip. Usage: `<button hhTooltip="Delete row">`. */
@Directive({
  selector: '[hhTooltip]',
  standalone: true,
  host: {
    '(mouseenter)': 'show()',
    '(mouseleave)': 'hide()',
    '(focus)': 'show()',
    '(blur)': 'hide()',
    '(keydown.escape)': 'hide()',
  },
})
export class HisHopeTooltipDirective implements OnDestroy {
  @Input('hhTooltip') text = '';
  @Input('hhTooltipPosition') position: HisHopeTooltipPosition = 'above';

  private readonly overlay = inject(Overlay);
  private readonly elementRef = inject(ElementRef<HTMLElement>);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  private overlayRef: OverlayRef | null = null;
  private showTimer: ReturnType<typeof setTimeout> | null = null;
  private readonly tooltipId = `hh-tooltip-${Math.random().toString(36).slice(2)}`;

  show(): void {
    if (!this.isBrowser || this.overlayRef || !this.text) return;
    this.showTimer = setTimeout(() => this.attach(), 300);
  }

  hide(): void {
    if (this.showTimer) {
      clearTimeout(this.showTimer);
      this.showTimer = null;
    }
    this.overlayRef?.dispose();
    this.overlayRef = null;
  }

  private attach(): void {
    this.elementRef.nativeElement.setAttribute('aria-describedby', this.tooltipId);
    const positionStrategy = this.overlay
      .position()
      .flexibleConnectedTo(this.elementRef)
      .withPositions([TOOLTIP_POSITIONS[this.position]]);
    this.overlayRef = this.overlay.create({
      positionStrategy,
      scrollStrategy: this.overlay.scrollStrategies.close(),
    });
    const ref = this.overlayRef.attach(new ComponentPortal(HisHopeTooltipPanelComponent));
    ref.instance.text = this.text;
    ref.instance.tooltipId = this.tooltipId;
  }

  ngOnDestroy(): void {
    this.hide();
  }
}
