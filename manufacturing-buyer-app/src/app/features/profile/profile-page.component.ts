import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  OnInit,
  inject,
} from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { FormsModule } from "@angular/forms";
import { CommerceApiService, Profile } from "../../core/services/commerce-api.service";
import { HisHopeTranslatePipe } from "@his-hope/frontend-foundation/i18n";

@Component({
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, HisHopeTranslatePipe],
  template: `
    <section class="page-shell page-shell--narrow">
      <div class="fx-container">
        <div class="page-head">
          <div>
            <p class="page-head__eyebrow">{{ 'buyer.profile.eyebrow' | hhTranslate }}</p>
            <h1>{{ 'buyer.profile.title' | hhTranslate }}</h1>
          </div>
        </div>

        @if (loading) {
          <div class="state">{{ 'buyer.profile.loading' | hhTranslate }}</div>
        } @else if (profile) {
          <form class="profile fx-card" (submit)="$event.preventDefault(); save()">
            <label>{{ 'buyer.profile.name' | hhTranslate }}<input [(ngModel)]="profile.displayName" name="displayName" /></label>
            <label>{{ 'buyer.profile.email' | hhTranslate }}<input [ngModel]="profile.email" name="email" disabled /></label>
            <label>{{ 'buyer.profile.phone' | hhTranslate }}<input [(ngModel)]="profile.phone" name="phone" /></label>
            <label>{{ 'buyer.profile.company' | hhTranslate }}<input [(ngModel)]="profile.companyName" name="companyName" /></label>
            <button type="submit" class="fx-btn-primary" [disabled]="saving">
              {{ saving ? ('buyer.saving' | hhTranslate) : ('buyer.profile.save' | hhTranslate) }}
            </button>
          </form>
        }
      </div>
    </section>
  `,
  styles: [
    `
      .page-head__eyebrow { margin: 0 0 0.35rem; color: var(--color-primary); font-weight: 800; text-transform: uppercase; letter-spacing: 0.08em; font-size: 0.78rem; }
      .page-head h1 { margin: 0 0 1.5rem; }
      .profile { padding: 1.25rem; display: grid; gap: 0.85rem; max-width: 520px; }
      label { display: grid; gap: 0.35rem; font-weight: 600; }
      input { padding: 0.75rem 0.85rem; border: 1px solid var(--border-default); border-radius: var(--radius-control); background: var(--surface-white); color: var(--text-primary); font: inherit; }
      input::placeholder { color: var(--text-secondary); }
      input:disabled { background: var(--surface-muted); color: var(--text-secondary); }
      input:focus-visible { outline: 2px solid var(--color-primary); outline-offset: 2px; }
      .state { padding: 2rem; text-align: center; color: var(--text-secondary); }
    `,
  ],
})
export class ProfilePageComponent implements OnInit {
  private readonly api = inject(CommerceApiService);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);

  loading = true;
  saving = false;
  profile: Profile | null = null;

  ngOnInit(): void {
    this.api.getProfile().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (profile) => {
        this.profile = profile;
        this.loading = false;
        this.cdr.markForCheck();
      },
      error: () => {
        this.loading = false;
        this.cdr.markForCheck();
      },
    });
  }

  save(): void {
    if (!this.profile) return;
    this.saving = true;
    this.api
      .updateProfile({
        displayName: this.profile.displayName,
        phone: this.profile.phone,
        companyName: this.profile.companyName,
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (profile) => {
          this.profile = profile;
          this.saving = false;
          this.cdr.markForCheck();
        },
        error: () => {
          this.saving = false;
          this.cdr.markForCheck();
        },
      });
  }
}
