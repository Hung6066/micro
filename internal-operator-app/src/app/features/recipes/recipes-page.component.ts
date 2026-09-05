import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  OnInit,
  inject,
} from "@angular/core";
import { CommonModule } from "@angular/common";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import {
  HisHopeActionButtonComponent,
  HisHopeDataTableCellDirective,
  HisHopeDataTableColumn,
  HisHopeResourceListPageComponent,
  HisHopeResourceRowActionsDirective,
  HisHopeDialogService,
  HisHopeWorkflowStepperComponent,
} from "@his-hope/frontend-foundation/ui";
import {
  HisHopeApiErrorMessageService as ApiErrorMessageService,
  HisHopeI18nService,
  HisHopeTranslatePipe,
} from "@his-hope/frontend-foundation/i18n";
import { HisHopeRecipeDto } from "@his-hope/frontend-foundation/contracts";
import { ManufacturingApiService } from "../../core/services/manufacturing-api.service";
import { TenantContextService } from "../../core/services/tenant-context.service";
import { buildEntityWorkflowSteps, buildReferenceWorkflowSteps } from "../../core/utils/manufacturing-workflow.util";
import { portalEnumLabel } from "../../core/utils/portal-label.util";
import { RecipeCreateDialogComponent } from "./recipe-create-dialog.component";

@Component({
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, HisHopeActionButtonComponent, HisHopeDataTableCellDirective, HisHopeResourceListPageComponent, HisHopeResourceRowActionsDirective, HisHopeTranslatePipe, HisHopeWorkflowStepperComponent],
  template: `
    <hh-resource-list-page
      title="customerPortal.recipesTitle"
      titleFallback="Recipe management"
      subtitle="customerPortal.tenantScope"
      [subtitleFallback]="pageSubtitle"
      countLabel="customerPortal.recipes"
      countLabelFallback="Recipes"
      tableLabel="customerPortal.recipes"
      tableLabelFallback="Recipes"
      [count]="recipes.length"
      [columns]="columns"
      [rows]="rows"
      [loading]="loading"
      [error]="error"
      (create)="openCreate()"
      (refresh)="load()"
    >
      <section hhResourceListPrefix class="workflow-reference" data-testid="recipe-workflow-reference">
        <h2 class="workflow-reference__title">{{ "customerPortal.workflowRecipe" | hhTranslate: "Recipe lifecycle" }}</h2>
        <hh-workflow-stepper [ariaLabel]="'customerPortal.workflowRecipe' | hhTranslate: 'Recipe lifecycle'" [steps]="referenceWorkflowSteps" />
      </section>
      <ng-template hhDataTableCell="workflow" let-row>
        <hh-workflow-stepper
          class="entity-workflow"
          [attr.data-testid]="'recipe-workflow-' + row['id']"
          [ariaLabel]="'customerPortal.workflowRecipe' | hhTranslate: 'Recipe lifecycle'"
          [steps]="recipeWorkflowSteps(recipeStatusFromRow(row))"
        />
      </ng-template>
      <ng-template hhResourceRowActions let-row>
        @if (row['status'] === 'Draft') {
          <hh-action-button kind="row" mode="icon-only" icon="send" [label]="'customerPortal.submitRecipe' | hhTranslate: 'Submit'" [disabled]="saving" (pressed)="transition(row, 'submit')" />
        }
        @if (row['status'] === 'Submitted') {
          <hh-action-button kind="primary" mode="icon-only" icon="verified" [label]="'customerPortal.approveRecipe' | hhTranslate: 'Approve'" [disabled]="saving" (pressed)="transition(row, 'approve')" />
        }
        @if (row['status'] === 'Approved') {
          <hh-action-button kind="row" mode="icon-only" icon="archive" [label]="'customerPortal.retireRecipe' | hhTranslate: 'Retire'" [disabled]="saving" (pressed)="transition(row, 'retire')" />
        }
      </ng-template>
    </hh-resource-list-page>
    @if (actionError) { <p class="action-error" role="alert">{{ actionError }}</p> }
  `,
  styles: [`:host { display: block; font-family: var(--font-sans); } .workflow-reference { margin-bottom: var(--space-lg); padding: var(--space-md); border: 1px solid var(--border-subtle); border-radius: var(--radius-md); background: var(--surface-subtle); } .workflow-reference__title { margin: 0 0 var(--space-sm); font-size: var(--font-size-caption); font-weight: var(--font-weight-semibold); color: var(--text-secondary); } .entity-workflow { overflow-x: auto; } .action-error { margin: var(--space-md) 0 0; color: var(--color-danger); }`],
})
export class RecipesPageComponent implements OnInit {
  private readonly api = inject(ManufacturingApiService);
  private readonly tenantContext = inject(TenantContextService);
  private readonly dialog = inject(HisHopeDialogService);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly errors = inject(ApiErrorMessageService);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);
  recipes: HisHopeRecipeDto[] = [];
  rows: Record<string, unknown>[] = [];
  loading = true;
  saving = false;
  error = "";
  actionError = "";
  actor = "operator";
  tenantLabel: string | null = null;

  get pageSubtitle(): string { this.i18n.locale(); return this.i18n.t("customerPortal.tenantScope", "Tenant: {{tenant}}", { tenant: this.tenantLabel ?? "—" }); }
  get referenceWorkflowSteps() { return buildReferenceWorkflowSteps(this.i18n, "recipe"); }
  recipeWorkflowSteps(status: string) { return buildEntityWorkflowSteps(this.i18n, "recipe", status); }
  recipeStatusFromRow(row: Record<string, unknown>): string {
    return String(row["rawStatus"] ?? row["status"] ?? "");
  }
  recipeStatusLabel(status: string): string { return portalEnumLabel(this.i18n, "governanceLifecycleStatus", status); }
  get columns(): HisHopeDataTableColumn[] {
    this.i18n.locale();
    return [
      { key: "productSku", label: this.i18n.t("customerPortal.productSku", "Product SKU"), sortable: true },
      { key: "version", label: this.i18n.t("customerPortal.recipeVersion", "Version"), sortable: true },
      { key: "processStep", label: this.i18n.t("customerPortal.processStep", "Process step"), sortable: true },
      { key: "targetYieldPercent", label: this.i18n.t("customerPortal.targetYield", "Target yield %"), sortable: true },
      { key: "componentSummary", label: this.i18n.t("customerPortal.recipeComponents", "Components") },
      { key: "status", label: this.i18n.t("common.status", "Status"), sortable: true },
      { key: "workflow", label: this.i18n.t("customerPortal.columnWorkflow", "Workflow"), sortable: false },
      { key: "createdAt", label: this.i18n.t("common.createdAt", "Created"), sortable: true },
      { key: "actions", label: this.i18n.t("common.actions", "Actions"), sortable: false, hideable: false },
    ];
  }
  ngOnInit(): void {
    this.tenantContext.activeTenantLabel$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((label) => { this.tenantLabel = label; this.cdr.markForCheck(); });
    this.tenantContext.activeTenantKey$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => this.load());
  }
  load(): void {
    this.loading = true; this.error = "";
    this.api.getRecipes().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (recipes) => {
        this.recipes = recipes ?? [];
        this.rows = this.recipes.map((recipe) => ({
          id: recipe.id,
          productSku: recipe.productSku,
          version: `v${recipe.version}`,
          processStep: recipe.processStep,
          targetYieldPercent: `${recipe.targetYieldPercent}%`,
          componentSummary: recipe.components.map((component) => `${component.ingredientSku} · ${component.quantity} ${component.uom}`).join("; "),
          status: this.recipeStatusLabel(recipe.status),
          rawStatus: recipe.status,
          createdAt: new Date(recipe.createdAt).toLocaleString(),
        }));
        this.loading = false; this.cdr.markForCheck();
      },
      error: (error) => { this.error = this.errors.message(error, "customerPortal.recipesLoadFailed"); this.loading = false; this.cdr.markForCheck(); },
    });
  }
  openCreate(): void {
    this.actionError = "";
    this.dialog.open(RecipeCreateDialogComponent, { width: "min(720px, calc(100vw - 2 * var(--space-lg)))", maxWidth: "calc(100vw - 2 * var(--space-lg))" }).afterClosed().pipe(takeUntilDestroyed(this.destroyRef)).subscribe((saved) => { if (saved) this.load(); });
  }
  transition(row: Record<string, unknown>, action: "submit" | "approve" | "retire"): void {
    const recipe = this.recipes.find((item) => item.id === String(row["id"] ?? ""));
    if (!recipe) return;
    this.saving = true; this.actionError = "";
    const request = action === "submit" ? this.api.submitRecipe(recipe.id, this.actor) : action === "approve" ? this.api.approveRecipe(recipe.id, this.actor) : this.api.retireRecipe(recipe.id, this.actor);
    request.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => { this.saving = false; this.load(); },
      error: (error) => { this.actionError = this.errors.message(error, "customerPortal.recipeActionFailed"); this.saving = false; this.cdr.markForCheck(); },
    });
  }
}
