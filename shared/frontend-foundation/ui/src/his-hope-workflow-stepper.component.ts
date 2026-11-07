import { ChangeDetectionStrategy, Component, computed, input } from "@angular/core";
import type { HisHopeWorkflowStepRenderModel } from "@his-hope/frontend-foundation/contracts";

@Component({
  selector: "hh-workflow-stepper",
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <nav class="hh-workflow-stepper" [attr.aria-label]="ariaLabel()">
      <ol class="hh-workflow-stepper__list">
        @for (step of resolvedSteps(); track step.key; let last = $last) {
          <li
            class="hh-workflow-stepper__item"
            [class.hh-workflow-stepper__item--complete]="step.state === 'complete'"
            [class.hh-workflow-stepper__item--current]="step.state === 'current'"
            [class.hh-workflow-stepper__item--upcoming]="step.state === 'upcoming'"
            [class.hh-workflow-stepper__item--cancelled]="step.state === 'cancelled'"
            [attr.aria-current]="step.state === 'current' || step.state === 'cancelled' ? 'step' : null"
          >
            <span class="hh-workflow-stepper__marker" aria-hidden="true">
              @if (step.state === "complete") {
                <span class="material-icons">check</span>
              } @else if (step.state === "cancelled") {
                <span class="material-icons">close</span>
              } @else {
                <span class="hh-workflow-stepper__dot"></span>
              }
            </span>
            <span class="hh-workflow-stepper__label">{{ step.label }}</span>
            @if (!last) {
              <span class="hh-workflow-stepper__connector" aria-hidden="true"></span>
            }
          </li>
        }
      </ol>
    </nav>
  `,
  styles: [
    `
      :host {
        display: block;
      }

      .hh-workflow-stepper__list {
        display: flex;
        flex-wrap: wrap;
        gap: var(--space-sm);
        list-style: none;
        margin: 0;
        padding: 0;
      }

      .hh-workflow-stepper__item {
        position: relative;
        display: inline-flex;
        align-items: center;
        gap: var(--space-xs);
        min-width: 0;
        color: var(--text-secondary);
        font-size: var(--font-size-caption);
      }

      .hh-workflow-stepper__item--complete,
      .hh-workflow-stepper__item--current {
        color: var(--text-primary);
      }

      .hh-workflow-stepper__item--cancelled {
        color: var(--color-danger, #b42318);
      }

      .hh-workflow-stepper__marker {
        display: inline-flex;
        align-items: center;
        justify-content: center;
        width: var(--size-icon-sm, 1.25rem);
        height: var(--size-icon-sm, 1.25rem);
        border-radius: var(--radius-full);
        background: var(--surface-muted, var(--surface-subtle));
      }

      .hh-workflow-stepper__item--complete .hh-workflow-stepper__marker,
      .hh-workflow-stepper__item--current .hh-workflow-stepper__marker {
        background: var(--color-primary-soft, rgba(31, 95, 70, 0.12));
        color: var(--color-primary);
      }

      .hh-workflow-stepper__item--cancelled .hh-workflow-stepper__marker {
        background: rgba(180, 35, 24, 0.12);
        color: var(--color-danger, #b42318);
      }

      .hh-workflow-stepper__marker .material-icons {
        font-size: var(--font-size-caption);
        line-height: 1;
      }

      .hh-workflow-stepper__dot {
        width: var(--space-xs);
        height: var(--space-xs);
        border-radius: var(--radius-full);
        background: currentColor;
      }

      .hh-workflow-stepper__label {
        white-space: nowrap;
      }

      .hh-workflow-stepper__connector {
        width: var(--space-lg);
        height: 1px;
        margin-inline: var(--space-2xs);
        background: var(--border-default);
      }

      .hh-workflow-stepper__item--complete .hh-workflow-stepper__connector {
        background: var(--color-primary);
      }
    `,
  ],
})
export class HisHopeWorkflowStepperComponent {
  readonly steps = input<readonly HisHopeWorkflowStepRenderModel[]>([]);
  readonly currentStatus = input("");
  readonly ariaLabel = input("Workflow");

  readonly resolvedSteps = computed(() => {
    const steps = this.steps();
    if (steps.length) {
      return steps;
    }

    const status = this.currentStatus();
    if (!status) {
      return [];
    }

    return [{ key: status, label: status, state: "current" as const }];
  });
}
