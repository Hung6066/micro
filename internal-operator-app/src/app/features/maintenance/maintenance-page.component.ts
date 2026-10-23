import { DatePipe } from "@angular/common";
import { FormsModule } from "@angular/forms";
import { ChangeDetectionStrategy, ChangeDetectorRef, Component, DestroyRef, OnInit, inject } from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { forkJoin } from "rxjs";
import {
  HisHopeActionButtonComponent,
  HisHopePageHeaderComponent,
  HisHopePageLayoutComponent,
  HisHopeStateComponent,
  HisHopeTabsComponent,
  HisHopeSelectComponent,
} from "@his-hope/frontend-foundation/ui";
import { HisHopeI18nService, HisHopeTranslatePipe } from "@his-hope/frontend-foundation/i18n";
import { HisHopeMaintenanceWorkOrderDto, HisHopeMachineTelemetryDto, HisHopeManufacturingMachineDto, HisHopeManufacturingDowntimeDto, HisHopeMaintenancePlanDto, HisHopeMachineCalibrationDto } from "@his-hope/frontend-foundation/contracts";
import { ManufacturingApiService } from "../../core/services/manufacturing-api.service";
import { HisHopeApiErrorMessageService as ApiErrorMessageService } from "@his-hope/frontend-foundation/i18n";
import { TenantContextService } from "../../core/services/tenant-context.service";
import { portalEnumLabel } from "../../core/utils/portal-label.util";

@Component({
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, FormsModule, HisHopeActionButtonComponent, HisHopePageHeaderComponent, HisHopePageLayoutComponent, HisHopeStateComponent, HisHopeTabsComponent, HisHopeTranslatePipe, HisHopeSelectComponent],
  template: `
    <hh-page-layout>
      <hh-page-header hhPageHeader [title]="'customerPortal.maintenanceTitle' | hhTranslate: 'Machine maintenance'" [subtitle]="pageSubtitle" />
      <hh-tabs label="Maintenance sections"><button role="tab" type="button" [attr.aria-selected]="activeTab === 'work-orders'" [class.active]="activeTab === 'work-orders'" (click)="selectTab('work-orders')">{{ 'customerPortal.maintenanceQueue' | hhTranslate: 'Work orders' }}</button><button role="tab" type="button" [attr.aria-selected]="activeTab === 'machines'" [class.active]="activeTab === 'machines'" (click)="selectTab('machines')">{{ 'customerPortal.machine' | hhTranslate: 'Machines' }}</button><button role="tab" type="button" [attr.aria-selected]="activeTab === 'plans'" [class.active]="activeTab === 'plans'" (click)="selectTab('plans')">{{ 'customerPortal.maintenancePlans' | hhTranslate: 'Plans & calibration' }}</button><button role="tab" type="button" [attr.aria-selected]="activeTab === 'downtime'" [class.active]="activeTab === 'downtime'" (click)="selectTab('downtime')">{{ 'customerPortal.downtimeControl' | hhTranslate: 'Downtime control' }}</button><button role="tab" type="button" [attr.aria-selected]="activeTab === 'history'" [class.active]="activeTab === 'history'" (click)="selectTab('history')">{{ 'customerPortal.maintenance' | hhTranslate: 'Maintenance history' }}</button></hh-tabs>
      @if (loading) {
        <hh-state kind="loading" [message]="'customerPortal.loadingMaintenance' | hhTranslate: 'Loading maintenance…'" />
      } @else if (error) {
        <hh-state kind="error" [message]="error" />
      } @else {
        <section class="section form-section" [class.tab-panel--hidden]="activeTab !== 'work-orders'">
          <div class="section-heading">
            <div>
              <p class="eyebrow">{{ 'customerPortal.preventiveMaintenance' | hhTranslate: 'Preventive maintenance' }}</p>
              <h2>{{ 'customerPortal.createMaintenanceWorkOrder' | hhTranslate: 'Schedule a work order' }}</h2>
            </div>
            <div class="heading-actions">
              <span class="count">{{ workOrders.length }}</span>
              <hh-action-button kind="secondary" icon="autorenew" [label]="'customerPortal.generateDueMaintenance' | hhTranslate: 'Generate due'" [disabled]="generating" (pressed)="generateDueWorkOrders()" />
            </div>
          </div>
          <form class="work-order-form" (ngSubmit)="createWorkOrder()">
            <label for="machineId">{{ 'customerPortal.machine' | hhTranslate: 'Machine' }}
              <hh-select id="machineId" name="machineId" [(ngModel)]="draft.machineId" required>
                <option value="">{{ 'customerPortal.selectMachine' | hhTranslate: 'Select machine' }}</option>
                @for (machine of machines; track machine.id) {
                  <option [value]="machine.id">{{ machine.code }} · {{ machine.name }}</option>
                }
              </hh-select>
            </label>
            <label>{{ 'customerPortal.dueAt' | hhTranslate: 'Due at' }}
              <input name="dueAt" type="datetime-local" [(ngModel)]="draft.dueAt" required />
            </label>
            <label>{{ 'customerPortal.assignedTo' | hhTranslate: 'Assigned to' }}
              <input name="assignedTo" [(ngModel)]="draft.assignedTo" [placeholder]="'customerPortal.technicianPlaceholder' | hhTranslate: 'Technician or team'" />
            </label>
            <label>{{ 'customerPortal.maintenanceNotes' | hhTranslate: 'Notes' }}
              <input name="notes" [(ngModel)]="draft.notes" [placeholder]="'customerPortal.maintenanceNotesPlaceholder' | hhTranslate: 'Scope or checklist'" />
            </label>
            <hh-action-button kind="primary" icon="add_task" type="submit" [label]="'customerPortal.scheduleMaintenance' | hhTranslate: 'Schedule'" [disabled]="saving" />
          </form>
          @if (actionError) { <p class="action-error" role="alert">{{ actionError }}</p> }
        </section>

        <section class="section" [class.tab-panel--hidden]="activeTab !== 'machines'">
          <div class="section-heading">
            <div>
              <p class="eyebrow">{{ 'customerPortal.machineCondition' | hhTranslate: 'Machine condition' }}</p>
              <h2>{{ 'customerPortal.telemetryTimeline' | hhTranslate: 'Telemetry timeline' }}</h2>
            </div>
          </div>
          @if (!machines.length) {
            <p class="empty">{{ 'customerPortal.noMachines' | hhTranslate: 'No machines configured.' }}</p>
          } @else {
            <div class="cards machine-cards">
              @for (machine of machines; track machine.id) {
                <article class="card machine-card">
                  <header>
                    <div>
                      <strong>{{ machine.code }}</strong>
                      <p class="meta">{{ machine.name }}</p>
                    </div>
                    <span class="status" [class.fault]="latestTelemetry(machine.id)?.state === 'Fault' || latestTelemetry(machine.id)?.state === 'UnplannedDown'">
                      {{ latestTelemetry(machine.id)?.state || maintenanceStatusLabel(machine.status) }}
                    </span>
                  </header>
                  @if (machineEdit.id === machine.id) {
                    <form class="work-order-form" (ngSubmit)="saveMachine()"><label>{{ 'customerPortal.machineCode' | hhTranslate: 'Machine code' }}<input name="machineCode" [(ngModel)]="machineEdit.code" required /></label><label>{{ 'common.name' | hhTranslate: 'Name' }}<input name="machineName" [(ngModel)]="machineEdit.name" required /></label><label>{{ 'customerPortal.status' | hhTranslate: 'Status' }}<input name="machineStatus" [(ngModel)]="machineEdit.status" required /></label><label><input name="machineActive" type="checkbox" [(ngModel)]="machineEdit.active" /> {{ 'common.active' | hhTranslate: 'Active' }}</label><hh-action-button kind="primary" icon="save" type="submit" [label]="'common.save' | hhTranslate: 'Save'" [disabled]="savingMachine" /></form>
                  } @else { <hh-action-button kind="secondary" icon="edit" [label]="'common.edit' | hhTranslate: 'Edit'" (pressed)="editMachine(machine)" /> }
                  @if (!machineTelemetry(machine.id).length) {
                    <p class="meta">{{ 'customerPortal.noTelemetry' | hhTranslate: 'No telemetry received.' }}</p>
                  } @else {
                    <ul class="telemetry-list">
                      @for (reading of machineTelemetry(machine.id); track reading.id) {
                        <li>
                          <span>{{ reading.state || reading.meterName || reading.source }}</span>
                          <span class="telemetry-value">
                            @if (reading.meterValue !== null && reading.meterValue !== undefined) { {{ reading.meterValue }} } 
                            {{ reading.meterName || '' }} · {{ reading.observedAt | date: 'short' }}
                          </span>
                        </li>
                      }
                    </ul>
                  }
                </article>
              }
            </div>
          }
        </section>

        <section class="section" [class.tab-panel--hidden]="activeTab !== 'plans'">
          <div class="section-heading"><div><p class="eyebrow">{{ 'customerPortal.maintenancePlans' | hhTranslate: 'Preventive maintenance plans' }}</p><h2>{{ 'customerPortal.planAndCalibration' | hhTranslate: 'Plans & calibration' }}</h2></div></div>
          <form class="work-order-form" (ngSubmit)="createPlan()"><label for="planMachine">{{ 'customerPortal.machine' | hhTranslate: 'Machine' }}<hh-select id="planMachine" name="planMachine" [(ngModel)]="planDraft.machineId" required><option value="">{{ 'customerPortal.selectMachine' | hhTranslate: 'Select machine' }}</option>@for (machine of machines; track machine.id) { <option [value]="machine.id">{{ machine.code }} · {{ machine.name }}</option> }</hh-select></label><label>{{ 'customerPortal.planCode' | hhTranslate: 'Plan code' }}<input name="planCode" [(ngModel)]="planDraft.planCode" required /></label><label>{{ 'customerPortal.frequencyDays' | hhTranslate: 'Frequency (days)' }}<input name="frequencyDays" type="number" min="1" [(ngModel)]="planDraft.frequencyDays" required /></label><label>{{ 'customerPortal.nextDueAt' | hhTranslate: 'Next due' }}<input name="planNextDue" type="datetime-local" [(ngModel)]="planDraft.nextDueAt" required /></label><label>{{ 'customerPortal.checklist' | hhTranslate: 'Checklist' }}<input name="planChecklist" [(ngModel)]="planDraft.checklist" /></label><hh-action-button kind="primary" icon="add_task" type="submit" [label]="'customerPortal.createPlan' | hhTranslate: 'Create plan'" [disabled]="planBusy" /></form>
          @if (plans.length) { <div class="cards">@for (plan of plans; track plan.id) { <article class="card"><header><strong>{{ plan.planCode }}</strong><span class="status">{{ plan.active ? ('common.active' | hhTranslate: 'Active') : ('common.inactive' | hhTranslate: 'Inactive') }}</span></header><p class="meta">{{ machineLabel(plan.machineId) }} · {{ plan.maintenanceType }} · {{ plan.frequencyDays }} {{ 'customerPortal.days' | hhTranslate: 'days' }}</p><p class="meta">{{ 'customerPortal.nextDueAt' | hhTranslate: 'Next due' }}: {{ plan.nextDueAt | date:'medium' }}</p></article> }</div> } @else { <p class="empty">{{ 'customerPortal.noMaintenancePlans' | hhTranslate: 'No maintenance plans.' }}</p> }
          <div class="section-heading"><h2>{{ 'customerPortal.calibration' | hhTranslate: 'Machine calibration' }}</h2></div>
          <form class="work-order-form" (ngSubmit)="createCalibration()"><label for="calMachine">{{ 'customerPortal.machine' | hhTranslate: 'Machine' }}<hh-select id="calMachine" name="calMachine" [(ngModel)]="calibrationDraft.machineId" required><option value="">{{ 'customerPortal.selectMachine' | hhTranslate: 'Select machine' }}</option>@for (machine of machines; track machine.id) { <option [value]="machine.id">{{ machine.code }} · {{ machine.name }}</option> }</hh-select></label><label>{{ 'customerPortal.calibrationType' | hhTranslate: 'Calibration type' }}<input name="calType" [(ngModel)]="calibrationDraft.calibrationType" required /></label><label>{{ 'customerPortal.certificateNumber' | hhTranslate: 'Certificate number' }}<input name="calCert" [(ngModel)]="calibrationDraft.certificateNumber" required /></label><label>{{ 'customerPortal.nextDueAt' | hhTranslate: 'Next due' }}<input name="calNextDue" type="datetime-local" [(ngModel)]="calibrationDraft.nextDueAt" required /></label><label>{{ 'customerPortal.provider' | hhTranslate: 'Provider' }}<input name="calProvider" [(ngModel)]="calibrationDraft.provider" /></label><hh-action-button kind="secondary" icon="verified" type="submit" [label]="'customerPortal.recordCalibration' | hhTranslate: 'Record calibration'" [disabled]="calibrationBusy" /></form>
          @if (calibrations.length) { <div class="cards">@for (calibration of calibrations; track calibration.id) { <article class="card"><header><strong>{{ calibration.calibrationType }}</strong><span class="status">{{ calibrationResultLabel(calibration.result) }}</span></header><p class="meta">{{ calibration.certificateNumber }} · {{ calibration.provider || '—' }}</p><p class="meta">{{ 'customerPortal.nextDueAt' | hhTranslate: 'Next due' }}: {{ calibration.nextDueAt | date:'medium' }}</p></article> }</div> } @else { <p class="empty">{{ 'customerPortal.noCalibrations' | hhTranslate: 'No calibration records.' }}</p> }
        </section>

        <section class="section form-section" [class.tab-panel--hidden]="activeTab !== 'downtime'">
          <div class="section-heading"><div><p class="eyebrow">{{ 'customerPortal.downtimeControl' | hhTranslate: 'Downtime control' }}</p><h2>{{ 'customerPortal.recordDowntime' | hhTranslate: 'Record machine downtime' }}</h2></div><span class="count">{{ openDowntimes.length }}</span></div>
          <form class="work-order-form" (ngSubmit)="openDowntime()">
            <label for="downtimeMachineId">{{ 'customerPortal.machine' | hhTranslate: 'Machine' }}<hh-select id="downtimeMachineId" name="downtimeMachineId" [(ngModel)]="downtimeDraft.machineId" required><option value="">{{ 'customerPortal.selectMachine' | hhTranslate: 'Select machine' }}</option>@for (machine of machines; track machine.id) { <option [value]="machine.id">{{ machine.code }} · {{ machine.name }}</option> }</hh-select></label>
            <label>{{ 'customerPortal.downtimeReason' | hhTranslate: 'Reason' }}<input name="reason" [(ngModel)]="downtimeDraft.reason" required /></label>
            <label>{{ 'customerPortal.downtimeStartedAt' | hhTranslate: 'Started at' }}<input name="startedAt" type="datetime-local" [(ngModel)]="downtimeDraft.startedAt" required /></label>
            <label>{{ 'customerPortal.maintenanceNotes' | hhTranslate: 'Notes' }}<input name="downtimeNotes" [(ngModel)]="downtimeDraft.notes" /></label>
            <hh-action-button kind="primary" icon="pause_circle" type="submit" [label]="'customerPortal.recordDowntime' | hhTranslate: 'Record downtime'" [disabled]="savingDowntime" />
          </form>
          @if (openDowntimes.length) { <div class="cards">@for (downtime of openDowntimes; track downtime.id) { <article class="card"><header><div><strong>{{ machineLabel(downtime.machineId) }}</strong><p class="meta">{{ downtime.reason }} · {{ downtime.startedAt | date:'medium' }}</p></div><span class="status fault">{{ maintenanceStatusLabel(downtime.status) }}</span></header><p class="meta">{{ downtime.notes }}</p><hh-action-button kind="secondary" icon="play_circle" [label]="'customerPortal.resolveDowntime' | hhTranslate: 'Resolve downtime'" [disabled]="savingDowntime" (pressed)="resolveDowntime(downtime)" /></article> }</div> } @else { <p class="empty">{{ 'customerPortal.noOpenDowntime' | hhTranslate: 'No open downtime.' }}</p> }
        </section>

        <section class="section" [class.tab-panel--hidden]="activeTab !== 'work-orders' && activeTab !== 'history'">
          <div class="section-heading">
            <div>
              <p class="eyebrow">{{ 'customerPortal.maintenanceQueue' | hhTranslate: 'Maintenance queue' }}</p>
              <h2>{{ 'customerPortal.workOrders' | hhTranslate: 'Work orders' }}</h2>
            </div>
            <hh-action-button [kind]="showOpenOnly ? 'primary' : 'secondary'" [pressedState]="showOpenOnly" icon="filter_alt" [label]="'customerPortal.openOnly' | hhTranslate: 'Open only'" (pressed)="toggleOpenFilter()" />
          </div>
          @if (!visibleWorkOrders.length) {
            <p class="empty">{{ 'customerPortal.noMaintenanceWorkOrders' | hhTranslate: 'No maintenance work orders.' }}</p>
          } @else {
            <div class="cards">
              @for (workOrder of visibleWorkOrders; track workOrder.id) {
                <article class="card" [class.overdue]="isOverdue(workOrder)">
                  <header>
                    <div>
                      <strong>{{ machineLabel(workOrder.machineId) }}</strong>
                      <p class="meta">{{ workOrder.maintenanceType }} · {{ workOrder.assignedTo || ('customerPortal.unassigned' | hhTranslate: 'Unassigned') }}</p>
                    </div>
                    <span class="status" [class.complete]="workOrder.status === 'Completed'">{{ maintenanceStatusLabel(workOrder.status) }}</span>
                  </header>
                  <p class="due">{{ 'customerPortal.dueAtValue' | hhTranslate: 'Due {{date}}' : { date: (workOrder.dueAt | date: 'medium') || '' } }}</p>
                  @if (workOrder.notes) { <p class="meta">{{ workOrder.notes }}</p> }
                  @if (workOrder.status === 'Open') {
                    <div class="completion-form">
                      <label>{{ 'customerPortal.technician' | hhTranslate: 'Technician' }}
                        <input [value]="technicianDrafts[workOrder.id] || ''" (input)="setTechnician(workOrder.id, $any($event.target).value)" />
                      </label>
                      <label>{{ 'customerPortal.evidence' | hhTranslate: 'Evidence' }}
                        <input [value]="evidenceDrafts[workOrder.id] || ''" (input)="setEvidence(workOrder.id, $any($event.target).value)" placeholder="photo:// or document reference" />
                      </label>
                      <hh-action-button kind="secondary" icon="task_alt" [label]="'customerPortal.completeMaintenance' | hhTranslate: 'Complete work order'" [disabled]="savingId === workOrder.id" (pressed)="completeWorkOrder(workOrder)" />
                    </div>
                  } @else if (workOrder.evidence) {
                    <p class="complete-note">{{ 'customerPortal.completedWithEvidence' | hhTranslate: 'Completed with evidence: {{evidence}}' : { evidence: workOrder.evidence } }}</p>
                  }
                </article>
              }
            </div>
          }
        </section>
      }
    </hh-page-layout>
  `,
  styles: [`
    :host { display: block; }
    .section { display: grid; gap: var(--space-md); }
    .tab-panel--hidden { display: none !important; }
    .form-section { padding: var(--space-md); border: 1px solid var(--border-subtle); border-radius: var(--radius-md); background: var(--surface-white); }
    .section-heading { display: flex; align-items: center; justify-content: space-between; gap: var(--space-md); }
    .heading-actions { display: flex; align-items: center; gap: var(--space-sm); }
    .section-heading h2 { margin: 0; color: var(--text-primary); }
    .eyebrow { margin: 0 0 var(--space-2xs); color: var(--text-secondary); font-size: var(--font-size-caption); text-transform: uppercase; letter-spacing: var(--tracking-overline); }
    .count { min-width: var(--control-height-sm); padding: var(--space-xs) var(--space-sm); border-radius: var(--radius-pill); background: var(--surface-subtle); color: var(--text-primary); text-align: center; }
    .work-order-form, .completion-form { display: grid; grid-template-columns: repeat(auto-fit, minmax(12rem, 1fr)); gap: var(--space-sm); align-items: end; }
    label { display: grid; gap: var(--space-2xs); color: var(--text-secondary); font-size: var(--font-size-caption); }
    input, select { width: 100%; box-sizing: border-box; border: 1px solid var(--border-default); border-radius: var(--radius-sm); padding: var(--space-xs); color: var(--text-primary); background: var(--surface-white); font: inherit; }
    .cards { display: grid; grid-template-columns: repeat(auto-fit, minmax(18rem, 1fr)); gap: var(--space-md); }
    .machine-cards { grid-template-columns: repeat(auto-fit, minmax(16rem, 1fr)); }
    .machine-card { gap: var(--space-sm); }
    .card { display: grid; gap: var(--space-sm); padding: var(--space-md); border: 1px solid var(--border-subtle); border-radius: var(--radius-md); background: var(--surface-white); }
    .card.overdue { border-color: var(--color-danger); }
    header { display: flex; justify-content: space-between; gap: var(--space-sm); align-items: flex-start; }
    header strong, .due { color: var(--text-primary); }
    .meta, .complete-note { margin: 0; color: var(--text-secondary); font-size: var(--font-size-caption); }
    .status { color: var(--text-secondary); font-size: var(--font-size-caption); }
    .status.complete { color: var(--color-success); }
    .status.fault { color: var(--color-danger); }
    .telemetry-list { display: grid; gap: var(--space-xs); margin: 0; padding: 0; list-style: none; }
    .telemetry-list li { display: flex; justify-content: space-between; gap: var(--space-sm); color: var(--text-primary); font-size: var(--font-size-caption); }
    .telemetry-value { color: var(--text-secondary); text-align: right; }
    .action-error { margin: 0; color: var(--color-danger); font-size: var(--font-size-caption); }
    .empty { color: var(--text-secondary); }
  `],
})
export class MaintenancePageComponent implements OnInit {
  activeTab = "work-orders";
  selectTab(tab: string): void { this.activeTab = tab; this.applyTabVisibility(); this.cdr.markForCheck(); }
  private applyTabVisibility(): void { /* tab panel visibility is controlled by Angular bindings */ }
  private readonly api = inject(ManufacturingApiService);
  private readonly tenantContext = inject(TenantContextService);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly errors = inject(ApiErrorMessageService);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);

  loading = true;
  saving = false;
  savingId: string | null = null;
  generating = false;
  error = "";
  actionError = "";
  tenantLabel: string | null = null;
  machines: HisHopeManufacturingMachineDto[] = [];
  workOrders: HisHopeMaintenanceWorkOrderDto[] = [];
  plans: HisHopeMaintenancePlanDto[] = [];
  calibrations: HisHopeMachineCalibrationDto[] = [];
  downtimes: HisHopeManufacturingDowntimeDto[] = [];
  telemetryByMachine: Record<string, HisHopeMachineTelemetryDto[]> = {};
  showOpenOnly = false;
  technicianDrafts: Record<string, string> = {};
  evidenceDrafts: Record<string, string> = {};
  savingDowntime = false;
  downtimeDraft = { machineId: "", reason: "", startedAt: new Date().toISOString().slice(0, 16), notes: "" };
  machineEdit: { id: string; code: string; name: string; status: string; active: boolean } = { id: "", code: "", name: "", status: "", active: true };
  savingMachine = false;
  draft = { machineId: "", dueAt: new Date(Date.now() + 86400000).toISOString().slice(0, 16), assignedTo: "", notes: "" };
  planBusy = false;
  calibrationBusy = false;
  planDraft = { machineId: "", planCode: "", maintenanceType: "Preventive", frequencyDays: 30, nextDueAt: new Date(Date.now() + 86400000).toISOString().slice(0, 16), checklist: "" };
  calibrationDraft = { machineId: "", calibrationType: "Scale", certificateNumber: "", nextDueAt: new Date(Date.now() + 31536000000).toISOString().slice(0, 16), provider: "" };

  get pageSubtitle(): string { this.i18n.locale(); return this.i18n.t("customerPortal.tenantScope", "Tenant: {{tenant}}", { tenant: this.tenantLabel ?? this.i18n.t("customerPortal.tenantUnknown", "—") }); }
  get visibleWorkOrders(): HisHopeMaintenanceWorkOrderDto[] { return this.showOpenOnly ? this.workOrders.filter((x) => x.status === "Open") : this.workOrders; }
  get openDowntimes(): HisHopeManufacturingDowntimeDto[] { return this.downtimes.filter((x) => x.status === "Open"); }

  ngOnInit(): void {
    this.tenantContext.activeTenantLabel$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((label) => { this.tenantLabel = label; this.cdr.markForCheck(); });
    this.tenantContext.activeTenantKey$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => this.load());
  }

  load(): void {
    this.loading = true; this.error = "";
    forkJoin({ machines: this.api.getMachines(), workOrders: this.api.getMaintenanceWorkOrders(), downtimes: this.api.getMachineDowntimes() }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: ({ machines, workOrders, downtimes }) => {
        this.machines = machines ?? []; this.workOrders = workOrders ?? []; this.downtimes = downtimes ?? [];
        this.api.getMaintenancePlans(undefined, true).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: (plans) => { this.plans = plans ?? []; this.cdr.markForCheck(); } });
        this.api.getMachineCalibrations(this.machines[0].id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: (items) => { this.calibrations = items ?? []; this.cdr.markForCheck(); } });
        if (!this.machines.length) { this.loading = false; this.cdr.markForCheck(); return; }
        forkJoin(this.machines.map((machine) => this.api.getMachineTelemetry(machine.id, 5))).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
          next: (telemetry) => { this.telemetryByMachine = Object.fromEntries(this.machines.map((machine, index) => [machine.id, telemetry[index] ?? []])); this.loading = false; this.cdr.markForCheck(); },
          error: (error) => { this.actionError = this.errors.message(error, "customerPortal.maintenanceLoadFailed"); this.loading = false; this.cdr.markForCheck(); },
        });
      },
      error: (error) => { this.error = this.errors.message(error, "customerPortal.maintenanceLoadFailed"); this.loading = false; this.cdr.markForCheck(); },
    });
  }

  machineLabel(machineId: string): string { const machine = this.machines.find((x) => x.id === machineId); return machine ? `${machine.code} · ${machine.name}` : machineId; }
  maintenanceStatusLabel(status: string): string { return portalEnumLabel(this.i18n, "maintenanceStatus", status); }
  calibrationResultLabel(result: string): string { return portalEnumLabel(this.i18n, "qualityTestResult", result); }
  createPlan(): void { const draft = this.planDraft; if (!draft.machineId || !draft.planCode.trim() || draft.frequencyDays < 1) return; this.planBusy = true; this.api.createMaintenancePlan(draft.machineId, { planCode: draft.planCode.trim(), maintenanceType: draft.maintenanceType, frequencyDays: draft.frequencyDays, nextDueAt: new Date(draft.nextDueAt).toISOString(), checklist: draft.checklist.trim() || undefined, createdBy: "operator" }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: (plan) => { this.plans = [plan, ...this.plans]; this.planBusy = false; this.cdr.markForCheck(); }, error: (error) => { this.actionError = this.errors.message(error, "customerPortal.maintenanceLoadFailed"); this.planBusy = false; this.cdr.markForCheck(); } }); }
  createCalibration(): void { const draft = this.calibrationDraft; if (!draft.machineId || !draft.calibrationType.trim() || !draft.certificateNumber.trim()) return; this.calibrationBusy = true; this.api.createMachineCalibration(draft.machineId, { calibrationType: draft.calibrationType.trim(), certificateNumber: draft.certificateNumber.trim(), calibratedAt: new Date().toISOString(), nextDueAt: new Date(draft.nextDueAt).toISOString(), provider: draft.provider.trim() || undefined, createdBy: "operator" }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: (calibration) => { this.calibrations = [calibration, ...this.calibrations]; this.calibrationBusy = false; this.cdr.markForCheck(); }, error: (error) => { this.actionError = this.errors.message(error, "customerPortal.maintenanceLoadFailed"); this.calibrationBusy = false; this.cdr.markForCheck(); } }); }
  machineTelemetry(machineId: string): HisHopeMachineTelemetryDto[] { return this.telemetryByMachine[machineId] ?? []; }
  latestTelemetry(machineId: string): HisHopeMachineTelemetryDto | undefined { return this.machineTelemetry(machineId)[0]; }
  editMachine(machine: HisHopeManufacturingMachineDto): void { this.machineEdit = { id: machine.id, code: machine.code, name: machine.name, status: machine.status, active: machine.active }; }
  saveMachine(): void { const draft = this.machineEdit; if (!draft.id || !draft.code.trim() || !draft.name.trim()) return; this.savingMachine = true; this.api.updateMachine(draft.id, { code: draft.code.trim(), name: draft.name.trim(), status: draft.status.trim(), active: draft.active }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: (updated) => { this.machines = this.machines.map((item) => item.id === updated.id ? updated : item); this.machineEdit = { id: "", code: "", name: "", status: "", active: true }; this.savingMachine = false; this.cdr.markForCheck(); }, error: (error) => { this.actionError = this.errors.message(error, "customerPortal.machineSaveFailed"); this.savingMachine = false; this.cdr.markForCheck(); } }); }
  isOverdue(workOrder: HisHopeMaintenanceWorkOrderDto): boolean { return workOrder.status === "Open" && new Date(workOrder.dueAt).getTime() < Date.now(); }
  toggleOpenFilter(): void { this.showOpenOnly = !this.showOpenOnly; }
  generateDueWorkOrders(): void {
    this.generating = true; this.actionError = "";
    this.api.generateDueMaintenanceWorkOrders().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (generated) => {
        const generatedIds = new Set((generated ?? []).map((x) => x.id));
        this.workOrders = [...(generated ?? []), ...this.workOrders.filter((x) => !generatedIds.has(x.id))];
        this.generating = false; this.cdr.markForCheck();
      },
      error: (error) => { this.actionError = this.errors.message(error, "customerPortal.maintenanceGenerateFailed"); this.generating = false; this.cdr.markForCheck(); },
    });
  }
  setTechnician(id: string, value: string): void { this.technicianDrafts[id] = value; }
  setEvidence(id: string, value: string): void { this.evidenceDrafts[id] = value; }

  createWorkOrder(): void {
    if (!this.draft.machineId || !this.draft.dueAt) return;
    this.saving = true; this.actionError = "";
    this.api.createMaintenanceWorkOrder(this.draft.machineId, { dueAt: new Date(this.draft.dueAt).toISOString(), maintenanceType: "Preventive", assignedTo: this.draft.assignedTo.trim() || undefined, notes: this.draft.notes.trim() || undefined }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (workOrder) => { this.workOrders = [workOrder, ...this.workOrders]; this.draft = { ...this.draft, assignedTo: "", notes: "" }; this.saving = false; this.cdr.markForCheck(); },
      error: (error) => { this.actionError = this.errors.message(error, "customerPortal.maintenanceCreateFailed"); this.saving = false; this.cdr.markForCheck(); },
    });
  }

  completeWorkOrder(workOrder: HisHopeMaintenanceWorkOrderDto): void {
    const technician = (this.technicianDrafts[workOrder.id] ?? "").trim();
    if (!technician) { this.actionError = this.i18n.t("customerPortal.technicianRequired", "Technician is required."); this.cdr.markForCheck(); return; }
    this.savingId = workOrder.id; this.actionError = "";
    this.api.completeMaintenanceWorkOrder(workOrder.machineId, workOrder.id, { technician, completedAt: new Date().toISOString(), evidence: this.evidenceDrafts[workOrder.id]?.trim() || undefined }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (updated) => { this.workOrders = this.workOrders.map((x) => x.id === updated.id ? updated : x); this.savingId = null; this.cdr.markForCheck(); },
      error: (error) => { this.actionError = this.errors.message(error, "customerPortal.maintenanceCompleteFailed"); this.savingId = null; this.cdr.markForCheck(); },
    });
  }

  openDowntime(): void {
    if (!this.downtimeDraft.machineId || !this.downtimeDraft.reason.trim()) { this.actionError = this.i18n.t("customerPortal.downtimeFormInvalid", "Machine and reason are required."); this.cdr.markForCheck(); return; }
    this.savingDowntime = true; this.actionError = "";
    this.api.openMachineDowntime(this.downtimeDraft.machineId, { reason: this.downtimeDraft.reason.trim(), startedAt: new Date(this.downtimeDraft.startedAt).toISOString(), notes: this.downtimeDraft.notes.trim() || undefined }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: (downtime) => { this.downtimes = [downtime, ...this.downtimes]; this.downtimeDraft = { ...this.downtimeDraft, reason: "", notes: "" }; this.savingDowntime = false; this.cdr.markForCheck(); }, error: (error) => { this.actionError = this.errors.message(error, "customerPortal.downtimeSaveFailed"); this.savingDowntime = false; this.cdr.markForCheck(); } });
  }

  resolveDowntime(downtime: HisHopeManufacturingDowntimeDto): void {
    this.savingDowntime = true; this.actionError = "";
    this.api.resolveMachineDowntime(downtime.machineId, downtime.id, { endedAt: new Date().toISOString() }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: (updated) => { this.downtimes = this.downtimes.map((item) => item.id === updated.id ? updated : item); this.savingDowntime = false; this.cdr.markForCheck(); }, error: (error) => { this.actionError = this.errors.message(error, "customerPortal.downtimeResolveFailed"); this.savingDowntime = false; this.cdr.markForCheck(); } });
  }
}
