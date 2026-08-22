import { ChangeDetectionStrategy, Component, input, signal } from '@angular/core';
import { HisHopeTranslatePipe } from '@his-hope/frontend-foundation/i18n';

export interface HisHopeTransferItem { id: string; label: string; disabled?: boolean; }

@Component({
  selector: 'hh-transfer-list',
  standalone: true,
  imports: [HisHopeTranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<div class="hh-transfer"><fieldset><legend>{{ availableLabel() | hhTranslate }}</legend>@for (item of available(); track item.id) { <label><input type="checkbox" [checked]="selected().has(item.id)" [disabled]="item.disabled" (change)="toggle(item.id)"> {{ item.label }}</label> }</fieldset><button type="button" (click)="moveSelected()" [disabled]="selected().size === 0">{{ moveLabel() | hhTranslate }}</button><fieldset><legend>{{ selectedLabel() | hhTranslate }}</legend>@for (item of chosen(); track item.id) { <span>{{ item.label }}</span> }</fieldset></div>`,
  styles: [`:host { display: block; }.hh-transfer { display: grid; grid-template-columns: 1fr auto 1fr; align-items: center; gap: var(--space-lg); } fieldset { min-height: 140px; display: grid; align-content: start; gap: var(--space-sm); padding: var(--space-md); border: 1px solid var(--border-default); } label, fieldset span { padding: var(--space-2xs); } button { min-height: var(--control-height-compact); } @media (max-width: 640px) { .hh-transfer { grid-template-columns: 1fr; } }`],
})
export class HisHopeTransferListComponent {
  readonly items = input<readonly HisHopeTransferItem[]>([]);
  readonly availableLabel = input('domain.available');
  readonly selectedLabel = input('domain.selected');
  readonly moveLabel = input('domain.moveSelected');
  readonly selected = signal(new Set<string>());
  available(): readonly HisHopeTransferItem[] { return this.items().filter(item => !this.selected().has(item.id)); }
  chosen(): readonly HisHopeTransferItem[] { return this.items().filter(item => this.selected().has(item.id)); }
  toggle(id: string): void { this.selected.update(current => { const next = new Set(current); next.has(id) ? next.delete(id) : next.add(id); return next; }); }
  moveSelected(): void { this.selected.update(current => new Set(current)); }
}
