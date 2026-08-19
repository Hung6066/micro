import { FocusTrap, FocusTrapFactory } from "@angular/cdk/a11y";
import { Overlay, OverlayRef } from "@angular/cdk/overlay";
import { ComponentPortal, ComponentType } from "@angular/cdk/portal";
import {
  Injectable,
  InjectionToken,
  Injector,
  runInInjectionContext,
} from "@angular/core";
import { Observable, Subject } from "rxjs";

export const HIS_HOPE_DIALOG_DATA = new InjectionToken<unknown>(
  "HIS_HOPE_DIALOG_DATA",
);

export interface HisHopeDialogConfig<D = unknown> {
  data?: D;
  ariaLabel?: string;
  disableClose?: boolean;
  panelClass?: string | string[];
  width?: string;
  maxWidth?: string;
}

export class HisHopeDialogRef<R = unknown> {
  private readonly afterClosedSubject = new Subject<R | undefined>();
  private focusTrap: FocusTrap | null = null;
  private previouslyFocused: HTMLElement | null = null;
  private closed = false;

  constructor(private readonly overlayRef: OverlayRef) {}

  /** @internal wired by HisHopeDialogService right after attach */
  registerFocusRestoration(
    focusTrap: FocusTrap,
    previouslyFocused: HTMLElement | null,
  ): void {
    this.focusTrap = focusTrap;
    this.previouslyFocused = previouslyFocused;
  }

  close(result?: R): void {
    if (this.closed) return;
    this.closed = true;
    this.afterClosedSubject.next(result);
    this.afterClosedSubject.complete();
    this.focusTrap?.destroy();
    this.overlayRef.dispose();
    this.previouslyFocused?.focus();
  }

  afterClosed(): Observable<R | undefined> {
    return this.afterClosedSubject.asObservable();
  }
}

/**
 * CDK-overlay-backed modal dialog service: focus trap, Escape/backdrop close,
 * scroll block and typed result. Compose feature dialog content with
 * `hh-create-dialog-shell`; this service owns only the overlay lifecycle.
 */
@Injectable({ providedIn: "root" })
export class HisHopeDialogService {
  constructor(private readonly injector: Injector) {}

  open<T, D = unknown, R = unknown>(
    component: ComponentType<T>,
    config: HisHopeDialogConfig<D> = {},
  ): HisHopeDialogRef<R> {
    const { overlay, focusTrapFactory } = runInInjectionContext(
      this.injector,
      () => ({
        overlay: this.injector.get(Overlay),
        focusTrapFactory: this.injector.get(FocusTrapFactory),
      }),
    );
    const previouslyFocused =
      typeof document === "undefined"
        ? null
        : (document.activeElement as HTMLElement | null);

    const panelClasses = Array.isArray(config.panelClass)
      ? config.panelClass
      : config.panelClass
        ? [config.panelClass]
        : [];

    const overlayRef = overlay.create({
      positionStrategy: overlay
        .position()
        .global()
        .centerHorizontally()
        .centerVertically(),
      hasBackdrop: true,
      backdropClass: "hh-dialog-cdk-backdrop",
      panelClass: ["hh-dialog-cdk-panel", ...panelClasses],
      width: config.width ?? "min(90vw, 720px)",
      minWidth: "min(90vw, 720px)",
      maxWidth: config.maxWidth ?? "95vw",
      maxHeight: "95vh",
      scrollStrategy: overlay.scrollStrategies.block(),
    });

    overlayRef.overlayElement.setAttribute("role", "dialog");
    overlayRef.overlayElement.setAttribute("aria-modal", "true");
    if (config.ariaLabel) {
      overlayRef.overlayElement.setAttribute("aria-label", config.ariaLabel);
    }

    const dialogRef = new HisHopeDialogRef<R>(overlayRef);
    const dialogInjector = Injector.create({
      parent: this.injector,
      providers: [
        { provide: HIS_HOPE_DIALOG_DATA, useValue: config.data },
        { provide: HisHopeDialogRef, useValue: dialogRef },
      ],
    });

    overlayRef.attach(new ComponentPortal(component, null, dialogInjector));
    const dialogHost = overlayRef.overlayElement
      .firstElementChild as HTMLElement | null;
    if (dialogHost) {
      dialogHost.style.display = "block";
      dialogHost.style.width = "100%";
      dialogHost.style.maxWidth = "100%";
    }

    const focusTrap = focusTrapFactory.create(overlayRef.overlayElement);
    focusTrap.focusInitialElementWhenReady();
    dialogRef.registerFocusRestoration(focusTrap, previouslyFocused);

    if (!config.disableClose) {
      overlayRef.backdropClick().subscribe(() => dialogRef.close());
      overlayRef.keydownEvents().subscribe((event) => {
        if (event.key === "Escape") dialogRef.close();
      });
    }

    return dialogRef;
  }
}
