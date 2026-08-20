import {
  ChangeDetectionStrategy,
  Component,
  EventEmitter,
  Input,
  Output,
} from "@angular/core";
import { CommonModule } from "@angular/common";
import {
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  Validators,
} from "@angular/forms";
import { MatButtonModule } from "@angular/material/button";
import { MatCardModule } from "@angular/material/card";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatInputModule } from "@angular/material/input";
import { MatSelectModule } from "@angular/material/select";
import { HisHopeTranslatePipe } from "@his-hope/frontend-foundation/i18n";
import {
  AccessRequest,
  AccessReview,
  BreakGlassRequest,
  PermissionDefinition,
  Role,
  User,
} from "../../core/services/access-governance-api.service";

import { HisHopeActionButtonComponent } from "@his-hope/frontend-foundation/ui";
@Component({
  selector: "app-access-governance-workflows",
  standalone: true,
  imports: [
    HisHopeActionButtonComponent,
    CommonModule,
    ReactiveFormsModule,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    HisHopeTranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (showJit) {
      <section class="grid two-col">
        <mat-card
          ><mat-card-header
            ><mat-card-title>{{
              "admin.requestAccess" | hhTranslate: "Request access"
            }}</mat-card-title></mat-card-header
          ><mat-card-content class="form-grid">
            <mat-form-field appearance="outline"
              ><mat-label>{{
                "admin.subject" | hhTranslate: "Subject"
              }}</mat-label
              ><mat-select [formControl]="requestForm.controls.subjectUserId"
                ><mat-option *ngFor="let user of users" [value]="user.id">{{
                  user.email || user.userName
                }}</mat-option></mat-select
              ></mat-form-field
            >
            <mat-form-field appearance="outline"
              ><mat-label>{{ "admin.roles" | hhTranslate: "Roles" }}</mat-label
              ><mat-select multiple [formControl]="requestForm.controls.roleIds"
                ><mat-option *ngFor="let role of roles" [value]="role.id">{{
                  role.name
                }}</mat-option></mat-select
              ></mat-form-field
            >
            <mat-form-field appearance="outline"
              ><mat-label>{{
                "admin.reason" | hhTranslate: "Reason"
              }}</mat-label
              ><textarea
                matInput
                rows="2"
                [formControl]="requestForm.controls.reason"
              ></textarea>
            </mat-form-field>
            <mat-form-field appearance="outline"
              ><mat-label>{{
                "admin.expiryHours" | hhTranslate: "Expiry hours"
              }}</mat-label
              ><input
                matInput
                type="number"
                min="1"
                max="72"
                [formControl]="requestForm.controls.expiryHours"
            /></mat-form-field>
            <hh-action-button
              [disabled]="busy || !canWrite || requestForm.invalid"
              (pressed)="submitRequest()"
              kind="primary"
              icon="add"
              [label]="
                'admin.createAccessRequest' | hhTranslate: 'Create request'
              "
            /> </mat-card-content
        ></mat-card>
        <mat-card
          ><mat-card-header
            ><mat-card-title>{{
              "admin.startAccessReview" | hhTranslate: "Start access review"
            }}</mat-card-title></mat-card-header
          ><mat-card-content class="form-grid">
            <mat-form-field appearance="outline"
              ><mat-label>{{
                "admin.subject" | hhTranslate: "Subject"
              }}</mat-label
              ><mat-select [formControl]="reviewForm.controls.subjectUserId"
                ><mat-option *ngFor="let user of users" [value]="user.id">{{
                  user.email || user.userName
                }}</mat-option></mat-select
              ></mat-form-field
            >
            <mat-form-field appearance="outline"
              ><mat-label>{{
                "admin.roles" | hhTranslate: "Roles to review"
              }}</mat-label
              ><mat-select multiple [formControl]="reviewForm.controls.roleIds"
                ><mat-option *ngFor="let role of roles" [value]="role.id">{{
                  role.name
                }}</mat-option></mat-select
              ></mat-form-field
            >
            <mat-form-field appearance="outline"
              ><mat-label>{{
                "admin.dueDays" | hhTranslate: "Due in days"
              }}</mat-label
              ><input
                matInput
                type="number"
                min="1"
                max="90"
                [formControl]="reviewForm.controls.dueDays"
            /></mat-form-field>
            <hh-action-button
              [disabled]="busy || !canWrite || reviewForm.invalid"
              (pressed)="submitReview()"
              kind="primary"
              icon="add"
              [label]="
                'admin.createAccessReview' | hhTranslate: 'Create review'
              "
            /> </mat-card-content
        ></mat-card>
      </section>
      <section class="grid">
        <mat-card
          ><mat-card-header
            ><mat-card-title>{{
              "admin.accessRequests" | hhTranslate: "Access requests"
            }}</mat-card-title></mat-card-header
          ><mat-card-content class="table-wrap"
            ><table>
              <thead>
                <tr>
                  <th>{{ "admin.subject" | hhTranslate: "Subject" }}</th>
                  <th>{{ "admin.status" | hhTranslate: "Status" }}</th>
                  <th>{{ "admin.expires" | hhTranslate: "Expires" }}</th>
                  <th>{{ "admin.actions" | hhTranslate: "Actions" }}</th>
                </tr>
              </thead>
              <tbody>
                <tr *ngFor="let item of requests">
                  <td>{{ displayUser(item.subjectUserId) }}</td>
                  <td>{{ item.status }}</td>
                  <td>{{ item.expiresAt | date: "short" }}</td>
                  <td>
                    <button
                      mat-button
                      color="primary"
                      *ngIf="item.status === 'pending'"
                      (click)="requestApproved.emit(item)"
                      [disabled]="busy || !canWrite"
                    >
                      {{ "admin.approve" | hhTranslate: "Approve" }}</button
                    ><button
                      mat-button
                      color="warn"
                      *ngIf="item.status === 'pending'"
                      (click)="requestRejected.emit(item)"
                      [disabled]="busy || !canWrite"
                    >
                      {{ "admin.reject" | hhTranslate: "Reject" }}
                    </button>
                  </td>
                </tr>
                <tr *ngIf="!requests.length">
                  <td colspan="4">
                    {{
                      "admin.noAccessRequests"
                        | hhTranslate: "No access requests."
                    }}
                  </td>
                </tr>
              </tbody>
            </table></mat-card-content
          ></mat-card
        >
        <mat-card
          ><mat-card-header
            ><mat-card-title>{{
              "admin.accessReviews" | hhTranslate: "Access reviews"
            }}</mat-card-title></mat-card-header
          ><mat-card-content class="table-wrap"
            ><table>
              <thead>
                <tr>
                  <th>{{ "admin.subject" | hhTranslate: "Subject" }}</th>
                  <th>{{ "admin.reviewer" | hhTranslate: "Reviewer" }}</th>
                  <th>{{ "admin.status" | hhTranslate: "Status" }}</th>
                  <th>{{ "admin.dueAt" | hhTranslate: "Due" }}</th>
                  <th>{{ "admin.actions" | hhTranslate: "Actions" }}</th>
                </tr>
              </thead>
              <tbody>
                <tr *ngFor="let item of reviews">
                  <td>{{ displayUser(item.subjectUserId) }}</td>
                  <td>{{ item.reviewer }}</td>
                  <td>{{ item.status }}</td>
                  <td>{{ item.dueAt | date: "short" }}</td>
                  <td>
                    <button
                      mat-button
                      color="primary"
                      *ngIf="item.status === 'pending'"
                      (click)="reviewCertified.emit(item)"
                      [disabled]="busy || !canWrite"
                    >
                      {{ "admin.certify" | hhTranslate: "Certify" }}</button
                    ><button
                      mat-button
                      color="warn"
                      *ngIf="item.status === 'pending'"
                      (click)="reviewRevoked.emit(item)"
                      [disabled]="busy || !canWrite"
                    >
                      {{ "admin.revoke" | hhTranslate: "Revoke" }}
                    </button>
                  </td>
                </tr>
                <tr *ngIf="!reviews.length">
                  <td colspan="5">
                    {{
                      "admin.noAccessReviews"
                        | hhTranslate: "No access reviews."
                    }}
                  </td>
                </tr>
              </tbody>
            </table></mat-card-content
          ></mat-card
        >
      </section>
    }
    @if (showBreakGlass) {
      <section class="grid two-col">
        <mat-card
          ><mat-card-header
            ><mat-card-title>{{
              "admin.breakGlass" | hhTranslate: "Break-glass access"
            }}</mat-card-title></mat-card-header
          ><mat-card-content class="form-grid">
            <mat-form-field appearance="outline"
              ><mat-label>{{
                "admin.subject" | hhTranslate: "Subject"
              }}</mat-label
              ><mat-select [formControl]="breakGlassForm.controls.subjectUserId"
                ><mat-option *ngFor="let user of users" [value]="user.id">{{
                  user.email || user.userName
                }}</mat-option></mat-select
              ></mat-form-field
            >
            <mat-form-field appearance="outline"
              ><mat-label>{{
                "admin.permission" | hhTranslate: "Permission"
              }}</mat-label
              ><mat-select
                [formControl]="breakGlassForm.controls.permissionCode"
                ><mat-option
                  *ngFor="let permission of permissions"
                  [value]="permission.code"
                  >{{ permission.name }} · {{ permission.code }}</mat-option
                ></mat-select
              ></mat-form-field
            >
            <mat-form-field appearance="outline"
              ><mat-label>{{
                "admin.facility" | hhTranslate: "Facility"
              }}</mat-label
              ><input
                matInput
                [formControl]="breakGlassForm.controls.facilityId"
            /></mat-form-field>
            <mat-form-field appearance="outline"
              ><mat-label>{{
                "admin.reason" | hhTranslate: "Reason"
              }}</mat-label
              ><textarea
                matInput
                rows="2"
                [formControl]="breakGlassForm.controls.reason"
              ></textarea>
            </mat-form-field>
            <hh-action-button
              [disabled]="busy || !canWrite || breakGlassForm.invalid"
              (pressed)="submitBreakGlass()"
              kind="danger"
              icon="link_off"
              [label]="
                'admin.requestBreakGlass' | hhTranslate: 'Request break-glass'
              "
            /> </mat-card-content
        ></mat-card>
        <mat-card
          ><mat-card-header
            ><mat-card-title>{{
              "admin.breakGlassRequests" | hhTranslate: "Break-glass requests"
            }}</mat-card-title></mat-card-header
          ><mat-card-content class="table-wrap"
            ><table>
              <thead>
                <tr>
                  <th>{{ "admin.subject" | hhTranslate: "Subject" }}</th>
                  <th>{{ "admin.permission" | hhTranslate: "Permission" }}</th>
                  <th>{{ "admin.status" | hhTranslate: "Status" }}</th>
                  <th>{{ "admin.expires" | hhTranslate: "Expires" }}</th>
                  <th>{{ "admin.actions" | hhTranslate: "Actions" }}</th>
                </tr>
              </thead>
              <tbody>
                <tr *ngFor="let item of breakGlassRequests">
                  <td>{{ displayUser(item.subjectUserId) }}</td>
                  <td>{{ item.permissionCode }}</td>
                  <td>{{ item.status }}</td>
                  <td>{{ item.expiresAt | date: "short" }}</td>
                  <td>
                    <button
                      mat-button
                      color="primary"
                      *ngIf="item.status === 'pending'"
                      (click)="breakGlassApproved.emit(item)"
                      [disabled]="busy || !canWrite"
                    >
                      {{ "admin.approve" | hhTranslate: "Approve" }}</button
                    ><button
                      mat-button
                      color="warn"
                      *ngIf="item.status === 'approved'"
                      (click)="breakGlassRevoked.emit(item)"
                      [disabled]="busy || !canWrite"
                    >
                      {{ "admin.revoke" | hhTranslate: "Revoke" }}
                    </button>
                  </td>
                </tr>
                <tr *ngIf="!breakGlassRequests.length">
                  <td colspan="5">
                    {{
                      "admin.noBreakGlassRequests"
                        | hhTranslate: "No break-glass requests."
                    }}
                  </td>
                </tr>
              </tbody>
            </table></mat-card-content
          ></mat-card
        >
      </section>
    }
  `,
  styles: [
    ":host{display:block}.grid{display:grid;gap:var(--space-4);margin-top:var(--space-4)}.two-col{grid-template-columns:repeat(2,minmax(0,1fr))}.form-grid{display:grid;gap:var(--space-3)}.table-wrap{overflow:auto}table{width:100%;border-collapse:collapse}th,td{text-align:left;padding:var(--space-2);border-bottom:1px solid var(--border-subtle);white-space:nowrap}@media(max-width:800px){.two-col{grid-template-columns:1fr}}",
  ],
})
export class AccessGovernanceWorkflowsComponent {
  @Input() users: User[] = [];
  @Input() roles: Role[] = [];
  @Input() permissions: PermissionDefinition[] = [];
  @Input() requests: AccessRequest[] = [];
  @Input() reviews: AccessReview[] = [];
  @Input() breakGlassRequests: BreakGlassRequest[] = [];
  @Input() showJit = true;
  @Input() showBreakGlass = true;
  @Input() busy = false;
  @Input() canWrite = false;
  @Input() displayUser: (id: string) => string = (id) => id;
  @Output() requestCreated = new EventEmitter<{
    subjectUserId: string;
    roleIds: string[];
    reason: string;
    expiryHours: number;
  }>();
  @Output() reviewCreated = new EventEmitter<{
    subjectUserId: string;
    roleIds: string[];
    dueDays: number;
  }>();
  @Output() requestApproved = new EventEmitter<AccessRequest>();
  @Output() requestRejected = new EventEmitter<AccessRequest>();
  @Output() reviewCertified = new EventEmitter<AccessReview>();
  @Output() reviewRevoked = new EventEmitter<AccessReview>();
  @Output() breakGlassCreated = new EventEmitter<{
    subjectUserId: string;
    permissionCode: string;
    facilityId: string;
    reason: string;
    durationMinutes: number;
  }>();
  @Output() breakGlassApproved = new EventEmitter<BreakGlassRequest>();
  @Output() breakGlassRevoked = new EventEmitter<BreakGlassRequest>();

  readonly requestForm = new FormGroup({
    subjectUserId: new FormControl("", {
      nonNullable: true,
      validators: Validators.required,
    }),
    roleIds: new FormControl<string[]>([], {
      nonNullable: true,
      validators: Validators.minLength(1),
    }),
    reason: new FormControl("", {
      nonNullable: true,
      validators: [Validators.required, Validators.minLength(10)],
    }),
    expiryHours: new FormControl(8, {
      nonNullable: true,
      validators: [Validators.required, Validators.min(1), Validators.max(72)],
    }),
  });
  readonly reviewForm = new FormGroup({
    subjectUserId: new FormControl("", {
      nonNullable: true,
      validators: Validators.required,
    }),
    roleIds: new FormControl<string[]>([], {
      nonNullable: true,
      validators: Validators.minLength(1),
    }),
    dueDays: new FormControl(30, {
      nonNullable: true,
      validators: [Validators.required, Validators.min(1), Validators.max(90)],
    }),
  });
  readonly breakGlassForm = new FormGroup({
    subjectUserId: new FormControl("", {
      nonNullable: true,
      validators: Validators.required,
    }),
    permissionCode: new FormControl("", {
      nonNullable: true,
      validators: Validators.required,
    }),
    facilityId: new FormControl("", {
      nonNullable: true,
      validators: Validators.required,
    }),
    reason: new FormControl("", {
      nonNullable: true,
      validators: [Validators.required, Validators.minLength(10)],
    }),
    durationMinutes: new FormControl(15, {
      nonNullable: true,
      validators: [Validators.required, Validators.min(1)],
    }),
  });

  submitRequest(): void {
    this.requestForm.markAllAsTouched();
    if (this.requestForm.invalid || !this.canWrite || this.busy) return;
    this.requestCreated.emit(this.requestForm.getRawValue());
    this.requestForm.reset({
      subjectUserId: "",
      roleIds: [],
      reason: "",
      expiryHours: 8,
    });
  }

  submitReview(): void {
    this.reviewForm.markAllAsTouched();
    if (this.reviewForm.invalid || !this.canWrite || this.busy) return;
    this.reviewCreated.emit(this.reviewForm.getRawValue());
    this.reviewForm.reset({ subjectUserId: "", roleIds: [], dueDays: 30 });
  }

  submitBreakGlass(): void {
    this.breakGlassForm.markAllAsTouched();
    if (this.breakGlassForm.invalid || !this.canWrite || this.busy) return;
    this.breakGlassCreated.emit(this.breakGlassForm.getRawValue());
    this.breakGlassForm.reset({
      subjectUserId: "",
      permissionCode: "",
      facilityId: "",
      reason: "",
      durationMinutes: 15,
    });
  }
}
