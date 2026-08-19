import {
  Directive,
  ElementRef,
  OnDestroy,
  Renderer2,
  computed,
  effect,
  inject,
  input,
  signal,
} from '@angular/core';

/**
 * Masks PHI/sensitive text content by default (dots), revealing only on
 * explicit click/keyboard activation. Auto re-masks after
 * `hhPhiMaskAutoHideMs` (default 8s) so a forgotten open tab doesn't leave
 * PHI visible indefinitely. This is a display-layer control only — the real
 * value still exists in the DOM/host binding; do not rely on it as the sole
 * access control for genuinely restricted data.
 */
@Directive({
  selector: '[hhPhiMask]',
  standalone: true,
  host: {
    class: 'hh-phi-mask',
    role: 'button',
    tabindex: '0',
    '[attr.aria-label]': 'ariaLabel()',
    '[attr.aria-pressed]': 'revealed()',
    '(click)': 'toggle()',
    '(keydown.enter)': 'toggle()',
    '(keydown.space)': 'onSpace($event)',
  },
})
export class HisHopePhiMaskDirective implements OnDestroy {
  readonly hhPhiMask = input.required<string>();
  readonly hhPhiMaskRevealLabel = input('Show sensitive information');
  readonly hhPhiMaskHideLabel = input('Hide sensitive information');
  readonly hhPhiMaskAutoHideMs = input(8000);

  private readonly elementRef = inject(ElementRef<HTMLElement>);
  private readonly renderer = inject(Renderer2);
  readonly revealed = signal(false);
  private autoHideTimer: ReturnType<typeof setTimeout> | null = null;

  readonly ariaLabel = computed(() =>
    this.revealed() ? this.hhPhiMaskHideLabel() : this.hhPhiMaskRevealLabel(),
  );

  constructor() {
    effect(() => {
      const text = this.revealed() ? this.hhPhiMask() : this.maskOf(this.hhPhiMask());
      this.renderer.setProperty(this.elementRef.nativeElement, 'textContent', text);
    });
  }

  toggle(): void {
    this.revealed.update((value) => !value);
    if (this.revealed()) this.scheduleAutoHide();
    else this.clearAutoHide();
  }

  onSpace(event: Event): void {
    event.preventDefault();
    this.toggle();
  }

  private maskOf(value: string): string {
    return value ? '\u2022'.repeat(Math.min(value.length, 12)) : '';
  }

  private scheduleAutoHide(): void {
    this.clearAutoHide();
    const ms = this.hhPhiMaskAutoHideMs();
    if (ms > 0) this.autoHideTimer = setTimeout(() => this.revealed.set(false), ms);
  }

  private clearAutoHide(): void {
    if (this.autoHideTimer) clearTimeout(this.autoHideTimer);
  }

  ngOnDestroy(): void {
    this.clearAutoHide();
  }
}
