/** Template-driven confirm state for `hh-confirm-dialog`. Phase 0 uses the
 * existing dialog component; a DialogService.confirm() API is Phase 4. */
export class AdminConfirmState {
  open = false;
  title = "common.confirmAction";
  message = "common.confirmContinue";
  confirmLabel = "common.yes";
  private action: (() => void) | null = null;

  ask(
    message: string,
    action: () => void,
    options?: { title?: string; confirmLabel?: string },
  ): void {
    this.message = message;
    this.title = options?.title ?? "common.confirmAction";
    this.confirmLabel = options?.confirmLabel ?? "common.yes";
    this.action = action;
    this.open = true;
  }

  confirm(): void {
    const action = this.action;
    this.cancel();
    action?.();
  }

  cancel(): void {
    this.open = false;
    this.action = null;
  }
}
