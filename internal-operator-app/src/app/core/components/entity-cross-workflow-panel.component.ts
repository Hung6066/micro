import { ChangeDetectionStrategy, ChangeDetectorRef, Component, DestroyRef, inject, input } from "@angular/core";
import { RouterLink } from "@angular/router";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { catchError, finalize, of, type Observable } from "rxjs";
import type { HisHopeCrossEntityWorkflowTraceDto } from "@his-hope/frontend-foundation/contracts";
import { HisHopeWorkflowStepperComponent } from "@his-hope/frontend-foundation/ui";
import { HisHopeActionButtonComponent } from "@his-hope/frontend-foundation/ui";
import { HisHopeTranslatePipe, HisHopeI18nService } from "@his-hope/frontend-foundation/i18n";
import { mapCrossEntityWorkflowToStepper } from "../utils/cross-entity-workflow.util";

@Component({
  selector: "app-entity-cross-workflow-panel",
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, HisHopeWorkflowStepperComponent, HisHopeActionButtonComponent, HisHopeTranslatePipe],
  template: `
    <div class="cross-workflow-panel" [attr.data-testid]="'entity-cross-workflow-' + entityId()">
      <hh-action-button
        kind="row"
        icon="hub"
        [attr.data-testid]="'entity-cross-workflow-toggle-' + entityId()"
        [label]="
          expanded
            ? ('customerPortal.hideCrossWorkflow' | hhTranslate: 'Hide supply chain workflow')
            : ('customerPortal.showCrossWorkflow' | hhTranslate: 'Show supply chain workflow')
        "
        [disabled]="loading"
        (pressed)="toggle()"
      />
      @if (expanded) {
        @if (loading) {
          <p class="meta">{{ "customerPortal.loadingCrossWorkflow" | hhTranslate: "Loading supply chain workflow…" }}</p>
        } @else if (error) {
          <p class="error" role="alert">{{ error }}</p>
        } @else if (stepperSteps.length) {
          <hh-workflow-stepper
            class="cross-workflow-stepper"
            [attr.data-testid]="'entity-cross-workflow-stepper-' + entityId()"
            [ariaLabel]="'customerPortal.crossWorkflowTitle' | hhTranslate: 'Supply chain workflow'"
            [steps]="stepperSteps"
          />
          <ul class="cross-workflow-links">
            @for (step of traceSteps; track step.entityId + step.key) {
              <li>
                <a [routerLink]="step.route">{{ step.title }}</a>
                <span class="meta">{{ step.status }}</span>
              </li>
            }
          </ul>
        } @else {
          <p class="meta">{{ "customerPortal.noCrossWorkflow" | hhTranslate: "No linked supply chain steps found." }}</p>
        }
      }
    </div>
  `,
  styles: [
    `
      :host {
        display: block;
      }
      .cross-workflow-panel {
        margin-top: var(--space-sm);
      }
      .cross-workflow-stepper {
        margin: var(--space-sm) 0;
        overflow-x: auto;
      }
      .cross-workflow-links {
        list-style: none;
        margin: 0;
        padding: 0;
        display: grid;
        gap: var(--space-xs);
      }
      .cross-workflow-links a {
        color: var(--color-primary);
        font-size: var(--font-size-caption);
        text-decoration: none;
      }
      .cross-workflow-links a:hover {
        text-decoration: underline;
      }
      .meta {
        margin: var(--space-xs) 0 0;
        color: var(--text-secondary);
        font-size: var(--font-size-caption);
      }
      .error {
        margin: var(--space-xs) 0 0;
        color: var(--color-danger);
        font-size: var(--font-size-caption);
      }
    `,
  ],
})
export class EntityCrossWorkflowPanelComponent {
  readonly entityType = input.required<string>();
  readonly entityId = input.required<string>();
  readonly loadTrace = input.required<(entityType: string, entityId: string) => Observable<HisHopeCrossEntityWorkflowTraceDto>>();

  private readonly destroyRef = inject(DestroyRef);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly i18n = inject(HisHopeI18nService);

  expanded = false;
  loading = false;
  error = "";
  stepperSteps: ReturnType<typeof mapCrossEntityWorkflowToStepper> = [];
  traceSteps: HisHopeCrossEntityWorkflowTraceDto["steps"] = [];
  private loaded = false;

  toggle(): void {
    this.expanded = !this.expanded;
    if (this.expanded && !this.loaded) {
      this.fetch();
    } else {
      this.cdr.markForCheck();
    }
  }

  private fetch(): void {
    this.loading = true;
    this.error = "";
    this.loadTrace()(this.entityType(), this.entityId())
      .pipe(
        catchError(() => {
          this.error = this.i18n.t("customerPortal.crossWorkflowLoadFailed", "Unable to load supply chain workflow.");
          return of(null);
        }),
        finalize(() => {
          this.loading = false;
          this.loaded = true;
          this.cdr.markForCheck();
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((trace) => {
        if (!trace) {
          this.stepperSteps = [];
          this.traceSteps = [];
        } else {
          this.stepperSteps = mapCrossEntityWorkflowToStepper(trace, this.i18n);
          this.traceSteps = trace.steps ?? [];
        }
        this.cdr.markForCheck();
      });
  }
}
