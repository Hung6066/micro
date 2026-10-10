import type { HisHopeEntityStatusHistoryDto } from "@his-hope/frontend-foundation/contracts";
import type { HisHopeTimelineItem } from "@his-hope/frontend-foundation/domain";
import type { HisHopeI18nService } from "@his-hope/frontend-foundation/i18n";

export function mapEntityStatusHistoryToTimeline(
  history: readonly HisHopeEntityStatusHistoryDto[],
  statusLabel: (status: string) => string,
  i18n: HisHopeI18nService,
): HisHopeTimelineItem[] {
  i18n.locale();
  return history.map((entry) => ({
    id: entry.id,
    title: statusLabel(entry.toStatus || entry.fromStatus),
    detail: entry.fromStatus
      ? i18n.t("customerPortal.statusTransition", "{{from}} → {{to}} · {{actor}}", {
          from: statusLabel(entry.fromStatus),
          to: statusLabel(entry.toStatus),
          actor: entry.actor,
        })
      : i18n.t("customerPortal.statusCreatedBy", "Created by {{actor}}", { actor: entry.actor }),
    date: new Date(entry.occurredAt).toLocaleString(),
  }));
}
