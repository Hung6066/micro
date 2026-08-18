import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  EventEmitter,
  HostListener,
  Output,
  ViewChild,
  effect,
  input,
  signal,
} from "@angular/core";
import { HisHopeTranslatePipe } from "@his-hope/frontend-foundation/i18n";

export interface HisHopeCommand {
  id: string;
  label: string;
  description?: string;
  keywords?: string[];
  disabled?: boolean;
}

@Component({
  selector: "hh-command-palette",
  standalone: true,
  imports: [HisHopeTranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (open()) {
      <div class="hh-command-backdrop" (click)="close()"></div>
      <section
        #panel
        class="hh-command"
        role="dialog"
        aria-modal="true"
        [attr.aria-labelledby]="titleId"
      >
        <h2 [id]="titleId" class="hh-visually-hidden">
          {{ "common.commandPalette" | hhTranslate }}
        </h2>
        <input
          #queryInput
          type="search"
          [value]="query()"
          (input)="query.set($any($event.target).value)"
          [placeholder]="'common.searchCommands' | hhTranslate"
          [attr.aria-label]="'common.searchCommands' | hhTranslate"
          autofocus
        />
        <div role="listbox" [attr.aria-label]="'common.commands' | hhTranslate">
          @for (command of filteredCommands(); track command.id) {
            <button
              type="button"
              role="option"
              [disabled]="command.disabled"
              (click)="choose(command)"
            >
              <strong>{{ command.label }}</strong>
              @if (command.description) {
                <small>{{ command.description }}</small>
              }
            </button>
          } @empty {
            <p class="hh-command__empty">
              {{ "common.noCommands" | hhTranslate }}
            </p>
          }
        </div>
      </section>
    }
  `,
  styles: [
    `
      :host {
        display: contents;
      }
      .hh-command-backdrop {
        position: fixed;
        inset: 0;
        z-index: 1300;
        background: rgba(15, 35, 25, 0.42);
      }
      .hh-command {
        position: fixed;
        top: 12vh;
        left: 50%;
        z-index: 1310;
        width: min(620px, calc(100vw - 32px));
        transform: translateX(-50%);
        overflow: hidden;
        border: 1px solid var(--border-strong);
        border-radius: var(--radius-card);
        background: var(--surface-white);
        box-shadow: var(--shadow-dialog);
      }
      .hh-command > input {
        width: 100%;
        min-height: 56px;
        border: 0;
        border-bottom: 1px solid var(--border-default);
        padding: 0 18px;
        background: transparent;
        color: var(--text-primary);
        font: inherit;
        outline: 0;
      }
      .hh-command [role="listbox"] {
        max-height: 52vh;
        overflow: auto;
        padding: 8px;
      }
      .hh-command button {
        display: grid;
        width: 100%;
        gap: 2px;
        min-height: 52px;
        border: 0;
        border-radius: var(--radius-button);
        padding: 10px 12px;
        background: transparent;
        color: var(--text-primary);
        text-align: left;
        font: inherit;
        cursor: pointer;
      }
      .hh-command button:hover,
      .hh-command button:focus-visible {
        background: var(--surface-hover);
        outline: 0;
      }
      .hh-command small,
      .hh-command__empty {
        color: var(--text-secondary);
        font-size: var(--font-size-caption);
      }
      .hh-command__empty {
        padding: 20px;
        text-align: center;
      }
      .hh-visually-hidden {
        position: absolute;
        width: 1px;
        height: 1px;
        overflow: hidden;
        clip: rect(0 0 0 0);
      }
    `,
  ],
})
export class HisHopeCommandPaletteComponent {
  readonly titleId = `hh-command-title-${Math.random().toString(36).slice(2)}`;
  readonly commands = input<HisHopeCommand[]>([]);
  readonly open = input(false);
  @Output() readonly selected = new EventEmitter<HisHopeCommand>();
  @Output() readonly closed = new EventEmitter<void>();
  readonly query = signal("");
  @ViewChild("panel") private panel?: ElementRef<HTMLElement>;
  private previousFocus: HTMLElement | null = null;
  constructor() {
    effect(() => {
      if (this.open()) {
        this.previousFocus =
          typeof document === "undefined"
            ? null
            : (document.activeElement as HTMLElement);
        setTimeout(() =>
          this.panel?.nativeElement
            .querySelector<HTMLElement>("input,button")
            ?.focus(),
        );
      }
    });
  }
  filteredCommands(): HisHopeCommand[] {
    const q = this.query().trim().toLowerCase();
    return this.commands().filter(
      (c) =>
        !q ||
        [c.label, c.description, ...(c.keywords ?? [])]
          .join(" ")
          .toLowerCase()
          .includes(q),
    );
  }
  choose(command: HisHopeCommand): void {
    if (!command.disabled) {
      this.selected.emit(command);
      this.close();
    }
  }
  close(): void {
    this.query.set("");
    this.closed.emit();
    setTimeout(() => this.previousFocus?.focus());
  }
  @HostListener("document:keydown.escape") onEscape(): void {
    if (this.open()) this.close();
  }
  @HostListener("document:keydown.tab", ["$event"]) onTab(event: Event): void {
    const keyboardEvent = event as KeyboardEvent;
    if (!this.open() || !this.panel) return;
    const focusables = Array.from(
      this.panel.nativeElement.querySelectorAll<HTMLElement>(
        'input,button,[tabindex]:not([tabindex="-1"])',
      ),
    );
    if (!focusables.length) return;
    const first = focusables[0];
    const last = focusables[focusables.length - 1];
    if (keyboardEvent.shiftKey && document.activeElement === first) {
      keyboardEvent.preventDefault();
      last.focus();
    } else if (!keyboardEvent.shiftKey && document.activeElement === last) {
      keyboardEvent.preventDefault();
      first.focus();
    }
  }
}
