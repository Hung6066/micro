import { ChangeDetectionStrategy, ChangeDetectorRef, Component, Input, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { forkJoin } from 'rxjs';
import { ActivatedRoute } from '@angular/router';
import { AccessRequest, AccessReview, AdminApiService, BreakGlassRequest, PermissionDefinition, Role, User } from '../../core/services/admin-api.service';
import { HisHopeI18nService, HisHopePageHeaderComponent, HisHopePageLayoutComponent, HisHopePermissionService, HisHopeTranslatePipe } from '@his-hope/frontend-foundation';

@Component({
  selector: 'app-access-governance-page',
  standalone: true,
  imports: [CommonModule, FormsModule, MatButtonModule, MatCardModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatSnackBarModule, HisHopePageHeaderComponent, HisHopePageLayoutComponent, HisHopeTranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <hh-page-layout>
      <hh-page-header hhPageHeader [title]="'admin.accessGovernance' | hhTranslate:'Access governance'" [subtitle]="'admin.accessGovernanceSubtitle' | hhTranslate:'Time-bound access requests, independent reviews and emergency elevation.'">
        <button mat-stroked-button type="button" (click)="reload()" [disabled]="busy">{{ 'admin.refresh' | hhTranslate:'Refresh' }}</button>
      </hh-page-header>

      <p class="notice">{{ 'admin.governanceBoundary' | hhTranslate:'Approval and certification are enforced by Identity Service. MFA, maker-checker and separation-of-duties checks remain server-side.' }}</p>
      <p class="error" *ngIf="error">{{ error }}</p>

      <section class="grid two-col" *ngIf="showJit">
        <mat-card>
          <mat-card-header><mat-card-title>{{ 'admin.requestAccess' | hhTranslate:'Request access' }}</mat-card-title></mat-card-header>
          <mat-card-content class="form-grid">
            <mat-form-field appearance="outline"><mat-label>{{ 'admin.subject' | hhTranslate:'Subject' }}</mat-label><mat-select [(ngModel)]="request.subjectUserId"><mat-option *ngFor="let user of users" [value]="user.id">{{ user.email || user.userName }}</mat-option></mat-select></mat-form-field>
            <mat-form-field appearance="outline"><mat-label>{{ 'admin.roles' | hhTranslate:'Roles' }}</mat-label><mat-select multiple [(ngModel)]="request.roleIds"><mat-option *ngFor="let role of roles" [value]="role.id">{{ role.name }}</mat-option></mat-select></mat-form-field>
            <mat-form-field appearance="outline"><mat-label>{{ 'admin.reason' | hhTranslate:'Reason' }}</mat-label><textarea matInput rows="2" [(ngModel)]="request.reason"></textarea></mat-form-field>
            <mat-form-field appearance="outline"><mat-label>{{ 'admin.expiryHours' | hhTranslate:'Expiry hours' }}</mat-label><input matInput type="number" min="1" max="72" [(ngModel)]="request.expiryHours"></mat-form-field>
            <button mat-flat-button color="primary" type="button" (click)="createRequest()" [disabled]="busy || !canWrite || !request.subjectUserId || !request.roleIds.length || request.reason.trim().length < 10">{{ 'admin.createAccessRequest' | hhTranslate:'Create request' }}</button>
          </mat-card-content>
        </mat-card>

        <mat-card>
          <mat-card-header><mat-card-title>{{ 'admin.startAccessReview' | hhTranslate:'Start access review' }}</mat-card-title></mat-card-header>
          <mat-card-content class="form-grid">
            <mat-form-field appearance="outline"><mat-label>{{ 'admin.subject' | hhTranslate:'Subject' }}</mat-label><mat-select [(ngModel)]="review.subjectUserId"><mat-option *ngFor="let user of users" [value]="user.id">{{ user.email || user.userName }}</mat-option></mat-select></mat-form-field>
            <mat-form-field appearance="outline"><mat-label>{{ 'admin.roles' | hhTranslate:'Roles to review' }}</mat-label><mat-select multiple [(ngModel)]="review.roleIds"><mat-option *ngFor="let role of roles" [value]="role.id">{{ role.name }}</mat-option></mat-select></mat-form-field>
            <mat-form-field appearance="outline"><mat-label>{{ 'admin.dueDays' | hhTranslate:'Due in days' }}</mat-label><input matInput type="number" min="1" max="90" [(ngModel)]="review.dueDays"></mat-form-field>
            <button mat-flat-button color="primary" type="button" (click)="createReview()" [disabled]="busy || !canWrite || !review.subjectUserId || !review.roleIds.length">{{ 'admin.createAccessReview' | hhTranslate:'Create review' }}</button>
          </mat-card-content>
        </mat-card>
      </section>

      <section class="grid" *ngIf="showJit">
        <mat-card><mat-card-header><mat-card-title>{{ 'admin.accessRequests' | hhTranslate:'Access requests' }}</mat-card-title></mat-card-header><mat-card-content class="table-wrap">
          <table><thead><tr><th>{{ 'admin.subject' | hhTranslate:'Subject' }}</th><th>{{ 'admin.status' | hhTranslate:'Status' }}</th><th>{{ 'admin.expires' | hhTranslate:'Expires' }}</th><th>{{ 'admin.actions' | hhTranslate:'Actions' }}</th></tr></thead><tbody>
            <tr *ngFor="let item of requests"><td>{{ displayUser(item.subjectUserId) }}</td><td>{{ item.status }}</td><td>{{ item.expiresAt | date:'short' }}</td><td><button mat-button color="primary" type="button" *ngIf="item.status === 'pending'" (click)="approve(item)" [disabled]="busy || !canWrite">{{ 'admin.approve' | hhTranslate:'Approve' }}</button><button mat-button color="warn" type="button" *ngIf="item.status === 'pending'" (click)="reject(item)" [disabled]="busy || !canWrite">{{ 'admin.reject' | hhTranslate:'Reject' }}</button></td></tr>
            <tr *ngIf="!requests.length"><td colspan="4">{{ 'admin.noAccessRequests' | hhTranslate:'No access requests.' }}</td></tr>
          </tbody></table>
        </mat-card-content></mat-card>
        <mat-card><mat-card-header><mat-card-title>{{ 'admin.accessReviews' | hhTranslate:'Access reviews' }}</mat-card-title></mat-card-header><mat-card-content class="table-wrap">
          <table><thead><tr><th>{{ 'admin.subject' | hhTranslate:'Subject' }}</th><th>{{ 'admin.reviewer' | hhTranslate:'Reviewer' }}</th><th>{{ 'admin.status' | hhTranslate:'Status' }}</th><th>{{ 'admin.dueAt' | hhTranslate:'Due' }}</th><th>{{ 'admin.actions' | hhTranslate:'Actions' }}</th></tr></thead><tbody>
            <tr *ngFor="let item of reviews"><td>{{ displayUser(item.subjectUserId) }}</td><td>{{ item.reviewer }}</td><td>{{ item.status }}</td><td>{{ item.dueAt | date:'short' }}</td><td><button mat-button color="primary" type="button" *ngIf="item.status === 'pending'" (click)="certify(item)" [disabled]="busy || !canWrite">{{ 'admin.certify' | hhTranslate:'Certify' }}</button><button mat-button color="warn" type="button" *ngIf="item.status === 'pending'" (click)="revoke(item)" [disabled]="busy || !canWrite">{{ 'admin.revoke' | hhTranslate:'Revoke' }}</button></td></tr>
            <tr *ngIf="!reviews.length"><td colspan="5">{{ 'admin.noAccessReviews' | hhTranslate:'No access reviews.' }}</td></tr>
          </tbody></table>
        </mat-card-content></mat-card>
      </section>
      <section class="grid two-col" *ngIf="showBreakGlass">
        <mat-card><mat-card-header><mat-card-title>{{ 'admin.breakGlass' | hhTranslate:'Break-glass access' }}</mat-card-title></mat-card-header><mat-card-content class="form-grid">
          <mat-form-field appearance="outline"><mat-label>{{ 'admin.subject' | hhTranslate:'Subject' }}</mat-label><mat-select [(ngModel)]="breakGlass.subjectUserId"><mat-option *ngFor="let user of users" [value]="user.id">{{ user.email || user.userName }}</mat-option></mat-select></mat-form-field>
          <mat-form-field appearance="outline"><mat-label>{{ 'admin.permission' | hhTranslate:'Permission' }}</mat-label><mat-select [(ngModel)]="breakGlass.permissionCode"><mat-option *ngFor="let permission of permissions" [value]="permission.code">{{ permission.code }}</mat-option></mat-select></mat-form-field>
          <mat-form-field appearance="outline"><mat-label>{{ 'admin.facility' | hhTranslate:'Facility' }}</mat-label><input matInput [(ngModel)]="breakGlass.facilityId"></mat-form-field>
          <mat-form-field appearance="outline"><mat-label>{{ 'admin.reason' | hhTranslate:'Reason' }}</mat-label><textarea matInput rows="2" [(ngModel)]="breakGlass.reason"></textarea></mat-form-field>
          <button mat-flat-button color="warn" type="button" (click)="createBreakGlass()" [disabled]="busy || !canWrite || !breakGlass.subjectUserId || !breakGlass.permissionCode || !breakGlass.facilityId || breakGlass.reason.trim().length < 10">{{ 'admin.requestBreakGlass' | hhTranslate:'Request break-glass' }}</button>
        </mat-card-content></mat-card>
        <mat-card><mat-card-header><mat-card-title>{{ 'admin.breakGlassRequests' | hhTranslate:'Break-glass requests' }}</mat-card-title></mat-card-header><mat-card-content class="table-wrap">
          <table><thead><tr><th>{{ 'admin.subject' | hhTranslate:'Subject' }}</th><th>{{ 'admin.permission' | hhTranslate:'Permission' }}</th><th>{{ 'admin.status' | hhTranslate:'Status' }}</th><th>{{ 'admin.expires' | hhTranslate:'Expires' }}</th><th>{{ 'admin.actions' | hhTranslate:'Actions' }}</th></tr></thead><tbody>
            <tr *ngFor="let item of breakGlassRequests"><td>{{ displayUser(item.subjectUserId) }}</td><td>{{ item.permissionCode }}</td><td>{{ item.status }}</td><td>{{ item.expiresAt | date:'short' }}</td><td><button mat-button color="primary" *ngIf="item.status === 'pending'" (click)="approveBreakGlass(item)" [disabled]="busy || !canWrite">{{ 'admin.approve' | hhTranslate:'Approve' }}</button><button mat-button color="warn" *ngIf="item.status === 'approved'" (click)="revokeBreakGlass(item)" [disabled]="busy || !canWrite">{{ 'admin.revoke' | hhTranslate:'Revoke' }}</button></td></tr>
            <tr *ngIf="!breakGlassRequests.length"><td colspan="5">{{ 'admin.noBreakGlassRequests' | hhTranslate:'No break-glass requests.' }}</td></tr>
          </tbody></table>
        </mat-card-content></mat-card>
      </section>
    </hh-page-layout>
  `,
  styles: [':host{display:block}.grid{display:grid;gap:var(--space-4);margin-top:var(--space-4)}.two-col{grid-template-columns:repeat(2,minmax(0,1fr))}.form-grid{display:grid;gap:var(--space-3)}.notice{padding:var(--space-3);border:1px solid var(--border-default);border-radius:var(--radius-card);background:var(--surface-muted);color:var(--text-secondary)}.error{color:var(--color-danger)}.table-wrap{overflow:auto}table{width:100%;border-collapse:collapse}th,td{text-align:left;padding:var(--space-2);border-bottom:1px solid var(--border-subtle);white-space:nowrap}@media(max-width:800px){.two-col{grid-template-columns:1fr}}'],
})
export class AccessGovernancePageComponent implements OnInit {
  private readonly api = inject(AdminApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly permissionService = inject(HisHopePermissionService);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly snack = inject(MatSnackBar);
  private readonly cdr = inject(ChangeDetectorRef);
  users: User[] = [];
  roles: Role[] = [];
  requests: AccessRequest[] = [];
  reviews: AccessReview[] = [];
  breakGlassRequests: BreakGlassRequest[] = [];
  permissions: PermissionDefinition[] = [];
  busy = false;
  error = '';
  request = { subjectUserId: '', roleIds: [] as string[], reason: '', expiryHours: 8 };
  review = { subjectUserId: '', roleIds: [] as string[], dueDays: 30 };
  breakGlass = { subjectUserId: '', permissionCode: '', facilityId: '', reason: '', durationMinutes: 15 };
  currentView = '';
  @Input() initialView = '';

  get showJit(): boolean { return !this.currentView || this.currentView === 'jit'; }
  get showBreakGlass(): boolean { return !this.currentView || this.currentView === 'break-glass'; }

  get canWrite(): boolean { return this.permissionService.has('admin.roles.write'); }
  ngOnInit(): void {
    const routeView = String(this.route.snapshot.data['governanceView'] ?? '');
    if (routeView) { this.currentView = routeView; this.reload(); return; }
    this.route.queryParamMap.subscribe(params => { this.currentView = this.initialView || (params.get('view') ?? ''); this.cdr.markForCheck(); });
    this.reload();
  }
  reload(): void { this.busy = true; this.error = ''; forkJoin({ users: this.api.getUsers(), roles: this.api.getRoles(), permissions: this.api.getPermissions(), requests: this.api.getAccessRequests(), reviews: this.api.getAccessReviews(), breakGlassRequests: this.api.getBreakGlassRequests() }).subscribe({ next: state => { this.users = state.users; this.roles = state.roles; this.permissions = state.permissions; this.requests = state.requests; this.reviews = state.reviews; this.breakGlassRequests = state.breakGlassRequests; this.busy = false; this.cdr.markForCheck(); }, error: () => this.fail('admin.loadAccessGovernanceFailed', 'Unable to load access governance data.') }); }
  displayUser(id: string): string { const user = this.users.find(item => item.id === id); return user?.email || user?.userName || id; }
  createRequest(): void { this.execute(() => this.api.createAccessRequest(this.request), 'admin.accessRequestCreated', 'Access request created.', () => this.request = { subjectUserId: '', roleIds: [], reason: '', expiryHours: 8 }); }
  createReview(): void { this.execute(() => this.api.createAccessReview(this.review), 'admin.accessReviewCreated', 'Access review created.', () => this.review = { subjectUserId: '', roleIds: [], dueDays: 30 }); }
  approve(item: AccessRequest): void { this.execute(() => this.api.approveAccessRequest(item.id), 'admin.accessRequestApproved', 'Access request approved.'); }
  reject(item: AccessRequest): void { this.execute(() => this.api.rejectAccessRequest(item.id), 'admin.accessRequestRejected', 'Access request rejected.'); }
  certify(item: AccessReview): void { this.execute(() => this.api.certifyAccessReview(item.id), 'admin.accessReviewCertified', 'Access review certified.'); }
  revoke(item: AccessReview): void { this.execute(() => this.api.revokeAccessReview(item.id), 'admin.accessReviewRevoked', 'Access review revoked.'); }
  createBreakGlass(): void { this.execute(() => this.api.createBreakGlassRequest(this.breakGlass), 'admin.breakGlassCreated', 'Break-glass request created.', () => this.breakGlass = { subjectUserId: '', permissionCode: '', facilityId: '', reason: '', durationMinutes: 15 }); }
  approveBreakGlass(item: BreakGlassRequest): void { this.execute(() => this.api.approveBreakGlassRequest(item.id), 'admin.breakGlassApproved', 'Break-glass request approved.'); }
  revokeBreakGlass(item: BreakGlassRequest): void { this.execute(() => this.api.revokeBreakGlassRequest(item.id), 'admin.breakGlassRevoked', 'Break-glass request revoked.'); }
  private execute<T>(operation: () => import('rxjs').Observable<T>, key: string, fallback: string, after?: () => void): void { this.busy = true; this.error = ''; operation().subscribe({ next: () => { after?.(); this.snack.open(this.i18n.t(key, fallback), this.i18n.t('admin.close', 'Close'), { duration: 3000 }); this.reload(); }, error: () => this.fail('admin.governanceOperationFailed', 'The server rejected this governance operation.') }); }
  private fail(key: string, fallback: string): void { this.error = this.i18n.t(key, fallback); this.busy = false; this.cdr.markForCheck(); }
}
