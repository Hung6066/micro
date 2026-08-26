import { Component, Inject, inject } from "@angular/core";
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from "@angular/forms";
import { HIS_HOPE_DIALOG_DATA, HisHopeDialogRef, HisHopeEntityDialogComponent } from "@his-hope/frontend-foundation/ui";
import { HisHopeI18nService, HisHopeTranslatePipe } from "@his-hope/frontend-foundation/i18n";
import { ManufacturingApiService } from "../../core/services/manufacturing-api.service";
import { TenantContextService } from "../../core/services/tenant-context.service";

@Component({
  selector: "app-recipe-create-dialog",
  standalone: true,
  imports: [ReactiveFormsModule, HisHopeEntityDialogComponent, HisHopeTranslatePipe],
  template: `
    <hh-entity-dialog
      [title]="'customerPortal.createRecipe'"
      titleFallback="Create recipe version"
      sectionTitle="customerPortal.recipeDetails"
      sectionTitleFallback="Recipe details"
      sectionDescription="customerPortal.recipeDetailsDescription"
      sectionDescriptionFallback="Define the product, process and target yield."
      [saving]="saving"
      [formGroup]="formGroup"
      cancelLabel="common.cancel"
      (save)="save()"
      (cancel)="dialogRef.close()"
    >
      <form class="dialog-form" [formGroup]="formGroup" (ngSubmit)="save()">
        <div class="form-grid">
          <label>{{ 'customerPortal.productSku' | hhTranslate: 'Product SKU' }}<input formControlName="productSku" required /></label>
          <label>{{ 'customerPortal.recipeVersion' | hhTranslate: 'Version' }}<input formControlName="version" type="number" min="1" required /></label>
          <label>{{ 'customerPortal.processStep' | hhTranslate: 'Process step' }}<input formControlName="processStep" required /></label>
          <label>{{ 'customerPortal.outputUom' | hhTranslate: 'Output UOM' }}<input formControlName="outputUom" required /></label>
          <label>{{ 'customerPortal.targetYield' | hhTranslate: 'Target yield %' }}<input formControlName="targetYieldPercent" type="number" min="0.01" max="100" step="0.01" required /></label>
          <label>{{ 'customerPortal.recipeActor' | hhTranslate: 'Actor' }}<input formControlName="actor" required /></label>
        </div>
        <label class="wide">{{ 'customerPortal.recipeComponents' | hhTranslate: 'Components (one SKU, quantity, UOM per line)' }}<textarea formControlName="componentsText" rows="5" required placeholder="RM-MANGO, 1, kg"></textarea></label>
        @if (error) { <p class="error" role="alert">{{ error }}</p> }
      </form>
    </hh-entity-dialog>
  `,
  styles: [`
    :host { display: block; }
    .dialog-form { display: grid; gap: var(--space-lg); }
    .form-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: var(--space-md); }
    label { display: grid; gap: var(--space-2xs); color: var(--text-primary); font-size: var(--font-size-caption); }
    input, textarea { width: 100%; box-sizing: border-box; border: 1px solid var(--border-default); border-radius: var(--radius-control); padding: var(--space-sm); background: var(--surface); color: var(--text-primary); font: inherit; }
    input:focus, textarea:focus { outline: var(--focus-ring-width) solid var(--color-focus); outline-offset: 1px; }
    .wide { grid-column: 1 / -1; }
    .error { margin: 0; color: var(--color-danger); }
    @media (max-width: 640px) { .form-grid { grid-template-columns: 1fr; } .wide { grid-column: auto; } }
  `],
})
export class RecipeCreateDialogComponent {
  readonly dialogRef = inject(HisHopeDialogRef<RecipeCreateDialogComponent>);
  private readonly api = inject(ManufacturingApiService);
  private readonly tenantContext = inject(TenantContextService);
  private readonly i18n = inject(HisHopeI18nService);
  saving = false;
  error = "";
  readonly formGroup = new FormGroup({
    productSku: new FormControl("", { nonNullable: true, validators: Validators.required }),
    version: new FormControl(1, { nonNullable: true, validators: [Validators.required, Validators.min(1)] }),
    processStep: new FormControl("drying", { nonNullable: true, validators: Validators.required }),
    outputUom: new FormControl("kg", { nonNullable: true, validators: Validators.required }),
    targetYieldPercent: new FormControl(80, { nonNullable: true, validators: [Validators.required, Validators.min(0.01), Validators.max(100)] }),
    actor: new FormControl("operator", { nonNullable: true, validators: Validators.required }),
    componentsText: new FormControl("", { nonNullable: true, validators: Validators.required }),
  });

  constructor(@Inject(HIS_HOPE_DIALOG_DATA) _data: unknown) {}

  save(): void {
    if (this.formGroup.invalid) {
      this.formGroup.markAllAsTouched();
      this.error = this.i18n.t("customerPortal.recipeFormInvalid", "Complete the required recipe fields.");
      return;
    }
    const form = this.formGroup.getRawValue();
    const tenantKey = this.tenantContext.getActiveTenantKey();
    const components = form.componentsText.split(/\r?\n/).map((line) => line.split(",").map((value) => value.trim())).filter((parts) => parts.length === 3 && parts[0] && Number(parts[1]) > 0 && parts[2]).map(([ingredientSku, quantity, uom]) => ({ ingredientSku, quantity: Number(quantity), uom }));
    if (!tenantKey || !form.productSku.trim() || !components.length) {
      this.error = this.i18n.t("customerPortal.recipeFormInvalid", "Tenant, product SKU, yield and at least one valid component are required.");
      return;
    }
    this.saving = true; this.error = "";
    this.api.createRecipe({ productSku: form.productSku.trim(), version: form.version, processStep: form.processStep.trim(), outputUom: form.outputUom.trim(), targetYieldPercent: form.targetYieldPercent, tenantKey, components, active: false, status: "Draft" }).subscribe({
      next: () => this.dialogRef.close(true),
      error: () => { this.error = this.i18n.t("customerPortal.recipeSaveFailed", "Unable to save recipe."); this.saving = false; },
    });
  }
}
