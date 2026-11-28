import { ChangeDetectionStrategy, Component, inject, signal } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { MatButtonModule } from "@angular/material/button";
import { HisHopeTranslatePipe } from "@his-hope/frontend-foundation/i18n";
import { ContentApiService } from "../services/content-api.service";

@Component({
  selector: "app-newsletter-signup",
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, MatButtonModule, HisHopeTranslatePipe],
  template: `
    <form class="newsletter" data-testid="buyer-newsletter" (ngSubmit)="submit()">
      <h4>{{ "buyer.newsletter.title" | hhTranslate }}</h4>
      <p>{{ "buyer.newsletter.subtitle" | hhTranslate }}</p>
      <div class="newsletter__row">
        <input
          [(ngModel)]="email"
          name="newsletterEmail"
          type="email"
          required
          [placeholder]="'buyer.newsletter.placeholder' | hhTranslate"
        />
        <button mat-flat-button color="primary" type="submit" [disabled]="submitting()">
          {{ (submitting() ? "buyer.processing" : "buyer.newsletter.submit") | hhTranslate }}
        </button>
      </div>
      @if (statusKey()) {
        <p class="newsletter__status" [class.newsletter__status--success]="submitted()">
          {{ statusKey() | hhTranslate }}
        </p>
      }
    </form>
  `,
  styles: [
    `
      .newsletter h4 {
        margin: 0 0 var(--space-2xs);
        font-size: var(--font-size-body);
        font-weight: var(--font-weight-bold);
      }
      .newsletter p {
        margin: 0 0 var(--space-sm);
        color: var(--text-secondary);
        font-size: var(--font-size-caption);
        line-height: var(--leading-body);
      }
      .newsletter__row {
        display: flex;
        gap: var(--space-sm);
        flex-wrap: wrap;
      }
      .newsletter__row input {
        flex: 1 1 12rem;
        min-width: 0;
        padding: 0.65rem 0.85rem;
        border: 1px solid var(--border-default);
        border-radius: var(--radius-input, var(--radius-button));
        background: var(--surface-white);
        color: var(--text-primary);
        font: inherit;
      }
      .newsletter__status {
        margin: var(--space-sm) 0 0;
        font-size: var(--font-size-caption);
        color: var(--color-primary-deep);
      }
      .newsletter__status--success {
        color: var(--color-success, var(--color-primary-deep));
      }
    `,
  ],
})
export class NewsletterSignupComponent {
  private readonly api = inject(ContentApiService);

  email = "";
  readonly submitting = signal(false);
  readonly submitted = signal(false);
  readonly statusKey = signal("");

  submit(): void {
    this.submitting.set(true);
    this.submitted.set(false);
    this.statusKey.set("");
    this.api.subscribeNewsletter(this.email.trim()).subscribe({
      next: () => {
        this.statusKey.set("buyer.newsletter.success");
        this.submitted.set(true);
        this.email = "";
        this.submitting.set(false);
      },
      error: () => {
        this.statusKey.set("buyer.newsletter.error");
        this.submitting.set(false);
      },
    });
  }
}
