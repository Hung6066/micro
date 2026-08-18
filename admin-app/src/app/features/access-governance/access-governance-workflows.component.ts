import {
  ChangeDetectionStrategy,
  Component,
  EventEmitter,
  Input,
  Output,
} from "@angular/core";
import { CommonModule } from "@angular/common";
import { FormsModule } from "@angular/forms";
import { MatButtonModule } from "@angular/material/button";
import { MatCardModule } from "@angular/material/card";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatInputModule } from "@angular/material/input";
import { MatSelectModule } from "@angular/material/select";
import { HisHopeTranslatePipe } from "@his-hope/frontend-foundation";
import {
  AccessRequest,
  AccessReview,
  BreakGlassRequest,
  PermissionDefinition,
  Role,
  User,
} from "../../core/services/access-governance-api.service";

@Component({
  selector: "app-access-governance-workflows",
  standalone: true,
  imports: [CommonModule, FormsModule, MatButtonModule, MatCardModule, MatFormFieldModule, MatInputModule, MatSelectModule, HisHopeTranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (showJit) {
      <section class="grid two-col">
        <mat-card><mat-card-header><mat-card-title>{{ "admin.requestAccess" | hhTranslate: "Request access" }}</mat-card-title></mat-card-header><mat-card-content class="form-grid">
          <mat-form-field appearance="outline"><mat-label>{{ "admin.subject" | hhTranslate: "Subject" }}</mat-label><mat-select [(ngModel)]="request.subjectUserId"><mat-option *ngFor="let user of users" [value]="user.id">{{ user.email || user.userName }}</mat-option></mat-select></mat-form-field>
          <mat-form-field appearance="outline"><mat-label>{{ "admin.roles" | hhTranslate: "Roles" }}</mat-label><mat-select multiple [(ngModel)]="request.roleIds"><mat-option *ngFor="let role of roles" [value]="role.id">{{ role.name }}</mat-option></mat-select></mat-form-field>
          <mat-form-field appearance="outline"><mat-label>{{ "admin.reason" | hhTranslate: "Reason" }}</mat-label><textarea matInput rows="2" [(ngModel)]="request.reason"></textarea></mat-form-field>
          <mat-form-field appearance="outline"><mat-label>{{ "admin.expiryHours" | hhTranslate: "Expiry hours" }}</mat-label><input matInput type="number" min="1" max="72" [(ngModel)]="request.expiryHours" /></mat-form-field>
          <button mat-flat-button color="primary" type="button" (click)="requestCreated.emit()" [disabled]="busy || !canWrite || !request.subjectUserId || !request.roleIds.length || request.reason.trim().length < 10">{{ "admin.createAccessRequest" | hhTranslate: "Create request" }}</button>
        </mat-card-content></mat-card>
        <mat-card><mat-card-header><mat-card-title>{{ "admin.startAccessReview" | hhTranslate: "Start access review" }}</mat-card-title></mat-card-header><mat-card-content class="form-grid">
          <mat-form-field appearance="outline"><mat-label>{{ "admin.subject" | hhTranslate: "Subject" }}</mat-label><mat-select [(ngModel)]="review.subjectUserId"><mat-option *ngFor="let user of users" [value]="user.id">{{ user.email || user.userName }}</mat-option></mat-select></mat-form-field>
          <mat-form-field appearance="outline"><mat-label>{{ "admin.roles" | hhTranslate: "Roles to review" }}</mat-label><mat-select multiple [(ngModel)]="review.roleIds"><mat-option *ngFor="let role of roles" [value]="role.id">{{ role.name }}</mat-option></mat-select></mat-form-field>
          <mat-form-field appearance="outline"><mat-label>{{ "admin.dueDays" | hhTranslate: "Due in days" }}</mat-label><input matInput type="number" min="1" max="90" [(ngModel)]="review.dueDays" /></mat-form-field>
          <button mat-flat-button color="primary" type="button" (click)="reviewCreated.emit()" [disabled]="busy || !canWrite || !review.subjectUserId || !review.roleIds.length">{{ "admin.createAccessReview" | hhTranslate: "Create review" }}</button>
        </mat-card-content></mat-card>
      </section>
      <section class="grid">
        <mat-card><mat-card-header><mat-card-title>{{ "admin.accessRequests" | hhTranslate: "Access requests" }}</mat-card-title></mat-card-header><mat-card-content class="table-wrap"><table><thead><tr><th>{{ "admin.subject" | hhTranslate: "Subject" }}</th><th>{{ "admin.status" | hhTranslate: "Status" }}</th><th>{{ "admin.expires" | hhTranslate: "Expires" }}</th><th>{{ "admin.actions" | hhTranslate: "Actions" }}</th></tr></thead><tbody><tr *ngFor="let item of requests"><td>{{ displayUser(item.subjectUserId) }}</td><td>{{ item.status }}</td><td>{{ item.expiresAt | date: "short" }}</td><td><button mat-button color="primary" *ngIf="item.status === 'pending'" (click)="requestApproved.emit(item)" [disabled]="busy || !canWrite">{{ "admin.approve" | hhTranslate: "Approve" }}</button><button mat-button color="warn" *ngIf="item.status === 'pending'" (click)="requestRejected.emit(item)" [disabled]="busy || !canWrite">{{ "admin.reject" | hhTranslate: "Reject" }}</button></td></tr><tr *ngIf="!requests.length"><td colspan="4">{{ "admin.noAccessRequests" | hhTranslate: "No access requests." }}</td></tr></tbody></table></mat-card-content></mat-card>
        <mat-card><mat-card-header><mat-card-title>{{ "admin.accessReviews" | hhTranslate: "Access reviews" }}</mat-card-title></mat-card-header><mat-card-content class="table-wrap"><table><thead><tr><th>{{ "admin.subject" | hhTranslate: "Subject" }}</th><th>{{ "admin.reviewer" | hhTranslate: "Reviewer" }}</th><th>{{ "admin.status" | hhTranslate: "Status" }}</th><th>{{ "admin.dueAt" | hhTranslate: "Due" }}</th><th>{{ "admin.actions" | hhTranslate: "Actions" }}</th></tr></thead><tbody><tr *ngFor="let item of reviews"><td>{{ displayUser(item.subjectUserId) }}</td><td>{{ item.reviewer }}</td><td>{{ item.status }}</td><td>{{ item.dueAt | date: "short" }}</td><td><button mat-button color="primary" *ngIf="item.status === 'pending'" (click)="reviewCertified.emit(item)" [disabled]="busy || !canWrite">{{ "admin.certify" | hhTranslate: "Certify" }}</button><button mat-button color="warn" *ngIf="item.status === 'pending'" (click)="reviewRevoked.emit(item)" [disabled]="busy || !canWrite">{{ "admin.revoke" | hhTranslate: "Revoke" }}</button></td></tr><tr *ngIf="!reviews.length"><td colspan="5">{{ "admin.noAccessReviews" | hhTranslate: "No access reviews." }}</td></tr></tbody></table></mat-card-content></mat-card>
      </section>
    }
    @if (showBreakGlass) {
      <section class="grid two-col"><mat-card><mat-card-header><mat-card-title>{{ "admin.breakGlass" | hhTranslate: "Break-glass access" }}</mat-card-title></mat-card-header><mat-card-content class="form-grid">
        <mat-form-field appearance="outline"><mat-label>{{ "admin.subject" | hhTranslate: "Subject" }}</mat-label><mat-select [(ngModel)]="breakGlass.subjectUserId"><mat-option *ngFor="let user of users" [value]="user.id">{{ user.email || user.userName }}</mat-option></mat-select></mat-form-field>
        <mat-form-field appearance="outline"><mat-label>{{ "admin.permission" | hhTranslate: "Permission" }}</mat-label><mat-select [(ngModel)]="breakGlass.permissionCode"><mat-option *ngFor="let permission of permissions" [value]="permission.code">{{ permission.code }}</mat-option></mat-select></mat-form-field>
        <mat-form-field appearance="outline"><mat-label>{{ "admin.facility" | hhTranslate: "Facility" }}</mat-label><input matInput [(ngModel)]="breakGlass.facilityId" /></mat-form-field>
        <mat-form-field appearance="outline"><mat-label>{{ "admin.reason" | hhTranslate: "Reason" }}</mat-label><textarea matInput rows="2" [(ngModel)]="breakGlass.reason"></textarea></mat-form-field>
        <button mat-flat-button color="warn" type="button" (click)="breakGlassCreated.emit()" [disabled]="busy || !canWrite || !breakGlass.subjectUserId || !breakGlass.permissionCode || !breakGlass.facilityId || breakGlass.reason.trim().length < 10">{{ "admin.requestBreakGlass" | hhTranslate: "Request break-glass" }}</button>
      </mat-card-content></mat-card>
      <mat-card><mat-card-header><mat-card-title>{{ "admin.breakGlassRequests" | hhTranslate: "Break-glass requests" }}</mat-card-title></mat-card-header><mat-card-content class="table-wrap"><table><thead><tr><th>{{ "admin.subject" | hhTranslate: "Subject" }}</th><th>{{ "admin.permission" | hhTranslate: "Permission" }}</th><th>{{ "admin.status" | hhTranslate: "Status" }}</th><th>{{ "admin.expires" | hhTranslate: "Expires" }}</th><th>{{ "admin.actions" | hhTranslate: "Actions" }}</th></tr></thead><tbody><tr *ngFor="let item of breakGlassRequests"><td>{{ displayUser(item.subjectUserId) }}</td><td>{{ item.permissionCode }}</td><td>{{ item.status }}</td><td>{{ item.expiresAt | date: "short" }}</td><td><button mat-button color="primary" *ngIf="item.status === 'pending'" (click)="breakGlassApproved.emit(item)" [disabled]="busy || !canWrite">{{ "admin.approve" | hhTranslate: "Approve" }}</button><button mat-button color="warn" *ngIf="item.status === 'approved'" (click)="breakGlassRevoked.emit(item)" [disabled]="busy || !canWrite">{{ "admin.revoke" | hhTranslate: "Revoke" }}</button></td></tr><tr *ngIf="!breakGlassRequests.length"><td colspan="5">{{ "admin.noBreakGlassRequests" | hhTranslate: "No break-glass requests." }}</td></tr></tbody></table></mat-card-content></mat-card></section>
    }
  `,
  styles: [":host{display:block}.grid{display:grid;gap:var(--space-4);margin-top:var(--space-4)}.two-col{grid-template-columns:repeat(2,minmax(0,1fr))}.form-grid{display:grid;gap:var(--space-3)}.table-wrap{overflow:auto}table{width:100%;border-collapse:collapse}th,td{text-align:left;padding:var(--space-2);border-bottom:1px solid var(--border-subtle);white-space:nowrap}@media(max-width:800px){.two-col{grid-template-columns:1fr}}"],
})
export class AccessGovernanceWorkflowsComponent {
  @Input() users: User[] = [];
  @Input() roles: Role[] = [];
  @Input() permissions: PermissionDefinition[] = [];
  @Input() requests: AccessRequest[] = [];
  @Input() reviews: AccessReview[] = [];
  @Input() breakGlassRequests: BreakGlassRequest[] = [];
  @Input() request = { subjectUserId: "", roleIds: [] as string[], reason: "", expiryHours: 8 };
  @Input() review = { subjectUserId: "", roleIds: [] as string[], dueDays: 30 };
  @Input() breakGlass = { subjectUserId: "", permissionCode: "", facilityId: "", reason: "", durationMinutes: 15 };
  @Input() showJit = true;
  @Input() showBreakGlass = true;
  @Input() busy = false;
  @Input() canWrite = false;
  @Input() displayUser: (id: string) => string = (id) => id;
  @Output() requestCreated = new EventEmitter<void>();
  @Output() reviewCreated = new EventEmitter<void>();
  @Output() requestApproved = new EventEmitter<AccessRequest>();
  @Output() requestRejected = new EventEmitter<AccessRequest>();
  @Output() reviewCertified = new EventEmitter<AccessReview>();
  @Output() reviewRevoked = new EventEmitter<AccessReview>();
  @Output() breakGlassCreated = new EventEmitter<void>();
  @Output() breakGlassApproved = new EventEmitter<BreakGlassRequest>();
  @Output() breakGlassRevoked = new EventEmitter<BreakGlassRequest>();
}
