import { ChangeDetectorRef, Component, effect, inject } from "@angular/core";
import { catchError, forkJoin, of } from "rxjs";
import { HisHopeI18nService, HisHopeTranslatePipe } from "@his-hope/frontend-foundation/i18n";
import { OperatorMobileApiService, type MachineDowntime, type MaintenanceWorkOrder, type LotSummary, type ProductionBatch } from "../../core/services/operator-mobile-api.service";
import { OperatorMobileTenantContextService } from "../../core/operator-mobile-tenant-context.service";
import { operatorMobileErrorMessage } from "../../core/operator-mobile-error.util";

@Component({ standalone: true, imports: [HisHopeTranslatePipe], templateUrl: "./shift-handover-page.component.html", styleUrls: ["./shift-handover-page.component.scss"] })
export class ShiftHandoverPageComponent {
  private readonly api = inject(OperatorMobileApiService);
  private readonly tenant = inject(OperatorMobileTenantContextService);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly cdr = inject(ChangeDetectorRef);
  startedBatches: ProductionBatch[] = [];
  holdLots: LotSummary[] = [];
  openDowntimes: MachineDowntime[] = [];
  overdueWorkOrders: MaintenanceWorkOrder[] = [];
  loading = false;
  error = "";

  constructor() {
    effect(() => {
      if (this.tenant.activeTenantKey?.()) void this.load();
      else this.clear();
    });
  }

  async load(): Promise<void> {
    if (this.loading) return;
    this.loading = true;
    this.error = "";
    forkJoin({
      batches: this.api.getProductionBatches("Started").pipe(catchError(() => of([]))),
      lots: this.api.getLots().pipe(catchError(() => of([]))),
      orders: this.api.getMaintenanceWorkOrders("Open").pipe(catchError(() => of([]))),
      machines: this.api.getMachines().pipe(catchError(() => of([]))),
    }).subscribe(({ batches, lots, orders, machines }) => {
      const downtimeReads = machines.map((machine) => this.api.getMachineDowntimes(machine.id).pipe(catchError(() => of([]))));
      forkJoin(downtimeReads.length ? downtimeReads : [of([] as MachineDowntime[])]).subscribe((downtimeGroups) => {
        this.startedBatches = batches;
        this.holdLots = lots.filter((lot) => lot.disposition.toLowerCase() === "hold" || lot.disposition.toLowerCase() === "quarantined");
        this.openDowntimes = downtimeGroups.flat();
        const now = Date.now();
        this.overdueWorkOrders = orders.filter((order) => Date.parse(order.dueAt) < now);
        this.loading = false;
        this.cdr.markForCheck();
      });
    }, (error) => {
      this.error = operatorMobileErrorMessage(this.i18n, error);
      this.loading = false;
      this.cdr.markForCheck();
    });
  }

  private clear(): void {
    this.startedBatches = [];
    this.holdLots = [];
    this.openDowntimes = [];
    this.overdueWorkOrders = [];
  }
}
