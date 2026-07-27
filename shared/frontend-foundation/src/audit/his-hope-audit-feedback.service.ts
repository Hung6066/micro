import { Injectable, inject } from '@angular/core';
import { HisHopeAuditFeedback } from '../contracts/his-hope-ui-contracts';
import { HisHopeToastService } from '../ui/his-hope-toast.service';

@Injectable({ providedIn: 'root' })
export class HisHopeAuditFeedbackService {
  private readonly toast = inject(HisHopeToastService);

  report(feedback: HisHopeAuditFeedback): void {
    const message = feedback.message ?? `${feedback.action} ${feedback.resource}`;
    if (feedback.outcome === 'failure') this.toast.error(message);
    else if (feedback.outcome === 'success') this.toast.success(message);
    else this.toast.info(message);
  }
}
