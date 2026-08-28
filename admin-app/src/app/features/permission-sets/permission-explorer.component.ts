import { CommonModule } from "@angular/common";
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  output,
} from "@angular/core";
import { FormsModule } from "@angular/forms";
import { PermissionDefinition } from "../../core/contracts/admin.contracts";
import { HisHopeI18nService } from "@his-hope/frontend-foundation/i18n";

interface PermissionGroup {
  key: string;
  label: string;
  permissions: PermissionDefinition[];
}

@Component({
  selector: "app-permission-explorer",
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="permission-explorer" aria-labelledby="permission-explorer-title">
      <div class="permission-explorer__header">
        <div>
          <h3 id="permission-explorer-title">{{ i18n.t("admin.permissions", "Permissions") }}</h3>
          <p>{{ selected().length }} {{ i18n.t("admin.selected", "selected") }} · {{ filteredCount() }} {{ i18n.t("admin.available", "available") }}</p>
        </div>
        <button type="button" class="permission-explorer__clear" (click)="clear()" [disabled]="!selected().length">
          {{ i18n.t("admin.clear", "Clear") }}
        </button>
      </div>
      <input
        class="permission-explorer__search"
        type="search"
        [(ngModel)]="search"
        [placeholder]="i18n.t('admin.searchPermissions', 'Search service, module, resource or action')"
        [attr.aria-label]="i18n.t('admin.searchPermissions', 'Search permissions')"
      />
      <div class="permission-explorer__groups">
        <details *ngFor="let group of groups(); trackBy: trackGroup" open>
          <summary>
            <span>{{ group.label }}</span>
            <span class="permission-explorer__count">{{ selectedIn(group) }}/{{ group.permissions.length }}</span>
          </summary>
          <label *ngFor="let permission of group.permissions; trackBy: trackPermission" class="permission-explorer__item">
            <input type="checkbox" [checked]="isSelected(permission.code)" (change)="toggle(permission.code)" />
            <span>
              <strong>{{ permission.code }}</strong>
              <small>{{ permission.name }}</small>
            </span>
          </label>
        </details>
      </div>
    </section>
  `,
  styles: [`
    :host { display:block; }
    .permission-explorer { display:grid; gap:var(--space-sm); }
    .permission-explorer__header { display:flex; align-items:flex-start; justify-content:space-between; gap:var(--space-md); }
    h3 { margin:0; color:var(--text-primary); font-size:var(--font-size-label); }
    p { margin:var(--space-2xs) 0 0; color:var(--text-secondary); font-size:var(--font-size-caption); }
    .permission-explorer__clear { border:0; background:transparent; color:var(--color-primary); cursor:pointer; font:inherit; font-size:var(--font-size-caption); }
    .permission-explorer__clear:disabled { color:var(--text-muted); cursor:not-allowed; }
    .permission-explorer__search { width:100%; box-sizing:border-box; min-height:var(--control-height); padding:0 var(--space-md); border:1px solid var(--border-default); border-radius:var(--radius-control); background:var(--surface-white); color:var(--text-primary); font:inherit; }
    .permission-explorer__search:focus-visible { outline:var(--focus-ring-width) solid var(--color-focus); outline-offset:var(--focus-ring-offset); }
    .permission-explorer__groups { display:grid; gap:var(--space-xs); max-height:300px; overflow:auto; padding-right:var(--space-2xs); }
    details { border:1px solid var(--border-subtle); border-radius:var(--radius-control); background:var(--surface-subtle); }
    summary { display:flex; justify-content:space-between; padding:var(--space-sm) var(--space-md); color:var(--text-primary); cursor:pointer; font-weight:var(--font-weight-semibold); list-style:none; }
    summary::-webkit-details-marker { display:none; }
    .permission-explorer__count { color:var(--text-secondary); font-size:var(--font-size-caption); font-weight:var(--font-weight-medium); }
    .permission-explorer__item { display:flex; align-items:flex-start; gap:var(--space-sm); padding:var(--space-xs) var(--space-md); color:var(--text-primary); cursor:pointer; }
    .permission-explorer__item:hover { background:var(--surface-hover); }
    .permission-explorer__item input { flex:0 0 auto; margin-top:var(--space-2xs); accent-color:var(--color-primary); }
    .permission-explorer__item span { display:grid; gap:var(--space-2xs); min-width:0; }
    .permission-explorer__item strong { overflow-wrap:anywhere; font-size:var(--font-size-caption); font-weight:var(--font-weight-semibold); }
    .permission-explorer__item small { color:var(--text-secondary); font-size:var(--font-size-caption); }
  `],
})
export class PermissionExplorerComponent {
  readonly i18n = inject(HisHopeI18nService);
  readonly permissions = input<PermissionDefinition[]>([]);
  readonly value = input<string[]>([]);
  readonly valueChange = output<string[]>();
  search = "";

  readonly selected = computed(() => this.value());
  readonly groups = computed<PermissionGroup[]>(() => {
    const query = this.search.trim().toLowerCase();
    const grouped = new Map<string, PermissionDefinition[]>();
    for (const permission of this.permissions()) {
      const haystack = `${permission.code} ${permission.name}`.toLowerCase();
      if (query && !haystack.includes(query)) continue;
      const parts = permission.code.split(".");
      // Legacy permissions use `resource.action` while newer permissions use
      // `service.module.resource.action`; group both shapes by their domain.
      const key = parts.length <= 2 ? parts[0] : parts.slice(0, 2).join(".");
      const list = grouped.get(key) ?? [];
      list.push(permission);
      grouped.set(key, list);
    }
    return [...grouped.entries()]
      .sort(([a], [b]) => a.localeCompare(b))
      .map(([key, permissions]) => ({
        key,
        label: key
          .split(".")
          .map((part) => part.replace(/[-_]/g, " ").replace(/\b\w/g, (letter) => letter.toUpperCase()))
          .join(" · "),
        permissions,
      }));
  });
  readonly filteredCount = computed(() => this.groups().reduce((total, group) => total + group.permissions.length, 0));

  isSelected(code: string): boolean { return this.selected().includes(code); }
  selectedIn(group: PermissionGroup): number { return group.permissions.filter((item) => this.isSelected(item.code)).length; }
  toggle(code: string): void {
    const next = this.isSelected(code) ? this.selected().filter((item) => item !== code) : [...this.selected(), code];
    this.valueChange.emit(next);
  }
  clear(): void { this.valueChange.emit([]); }
  trackGroup(_: number, group: PermissionGroup): string { return group.key; }
  trackPermission(_: number, permission: PermissionDefinition): string { return permission.code; }
}
