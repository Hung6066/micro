import { ChangeDetectionStrategy, Component, inject, signal } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { MatButtonModule } from "@angular/material/button";
import {
  HisHopeApiErrorMessageService,
  HisHopeTranslatePipe,
} from "@his-hope/frontend-foundation/i18n";
import { ContentApiService } from "../../core/services/content-api.service";

@Component({
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, MatButtonModule, HisHopeTranslatePipe],
  templateUrl: "./cooperation-page.component.html",
  styleUrls: ["./cooperation-page.component.scss"],
})
export class CooperationPageComponent {
  private readonly api = inject(ContentApiService);
  private readonly errors = inject(HisHopeApiErrorMessageService);
  companyName = "";
  contactName = "";
  email = "";
  phone = "";
  partnershipType = "distributor";
  message = "";
  readonly submitting = signal(false);
  readonly submitted = signal(false);
  readonly statusKey = signal("");

  submit(): void {
    this.submitting.set(true);
    this.submitted.set(false);
    this.statusKey.set("");
    this.api
      .submitPartnershipInquiry({
        companyName: this.companyName,
        contactName: this.contactName,
        email: this.email,
        phone: this.phone,
        partnershipType: this.partnershipType,
        message: this.message,
      })
      .subscribe({
        next: () => {
          this.statusKey.set("buyer.cooperation.success");
          this.submitted.set(true);
          this.submitting.set(false);
        },
        error: (error) => {
          this.statusKey.set(
            this.errors.message(error, "buyer.cooperation.error"),
          );
          this.submitting.set(false);
        },
      });
  }
}
