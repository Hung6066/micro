import { ChangeDetectionStrategy, Component, EventEmitter, OnDestroy, Output, input } from '@angular/core';
import { HisHopeTranslatePipe } from '@his-hope/frontend-foundation/i18n';

@Component({
  selector: 'hh-filter-toolbar',
  standalone: true,
  imports: [HisHopeTranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="hh-filter-toolbar" [attr.aria-label]="label() | hhTranslate">
      @if (searchPlaceholder()) {
        <label class="hh-filter-toolbar__search">
          <span class="material-icons" aria-hidden="true">search</span>
          <input type="search" [value]="query" [placeholder]="searchPlaceholder() | hhTranslate"
                 [attr.aria-label]="searchLabel() | hhTranslate" (input)="onSearch($event)" />
          @if (query) {
            <button type="button" class="hh-filter-toolbar__clear" (click)="clearSearch()" [attr.aria-label]="clearLabel() | hhTranslate">
              <span class="material-icons" aria-hidden="true">close</span>
            </button>
          }
        </label>
      }
      <div class="hh-filter-toolbar__controls"><ng-content /></div>
      @if (resultCount() !== null) {
        <span class="hh-filter-toolbar__count">{{ resultCount() }} {{ 'common.results' | hhTranslate }}</span>
      }
    </section>
  `,
  styles: [`
    :host { display: block; }
    .hh-filter-toolbar__search { display: inline-flex; align-items: center; gap: 8px; min-height: var(--control-height, 40px); padding: 0 10px; border: 1px solid var(--border-default); border-radius: var(--radius-input); background: var(--surface-white); }
    .hh-filter-toolbar__search .material-icons { color: var(--text-muted); font-size: 18px; }
    .hh-filter-toolbar__search input { width: min(280px, 40vw); border: 0; outline: 0; background: transparent; color: var(--text-primary); font: inherit; }
    .hh-filter-toolbar__clear { display: grid; place-items: center; width: 28px; height: 28px; border: 0; border-radius: var(--radius-button); background: transparent; color: var(--text-secondary); cursor: pointer; }
    .hh-filter-toolbar__clear:hover { background: var(--surface-muted); }
    @media (max-width: 640px) { .hh-filter-toolbar__search { width: 100%; } .hh-filter-toolbar__search input { width: 100%; } }
  `],
})
export class HisHopeFilterToolbarComponent implements OnDestroy {
  readonly label = input('common.filters');
  readonly resultCount = input<number | null>(null);
  readonly searchPlaceholder = input('');
  readonly searchLabel = input('common.search');
  readonly clearLabel = input('common.clearSearch');
  readonly debounceMs = input(250);
  @Output() readonly searchChanged = new EventEmitter<string>();
  query = '';
  private searchTimer: ReturnType<typeof setTimeout> | undefined;

  onSearch(event: Event): void {
    this.query = (event.target as HTMLInputElement).value;
    if (this.searchTimer) clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => this.searchChanged.emit(this.query.trim()), this.debounceMs());
  }

  clearSearch(): void {
    this.query = '';
    this.searchChanged.emit('');
  }

  ngOnDestroy(): void {
    if (this.searchTimer) clearTimeout(this.searchTimer);
  }
}
