import { ChangeDetectionStrategy, ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { AdminApiService, AccessReview, Role, User } from '../../core/services/admin-api.service';
import { HisHopeI18nService, HisHopePageHeaderComponent, HisHopePageLayoutComponent, HisHopePermissionService, HisHopeTranslatePipe } from '@his-hope/frontend-foundation';

@Component({
  selector: 'app-access-reviews-page', standalone: true,
  imports: [CommonModule, FormsModule, MatButtonModule, MatCardModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatSnackBarModule, HisHopePageHeaderComponent, HisHopePageLayoutComponent, HisHopeTranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <hh-page-layout><hh-page-header hhPageHeader [title]="'admin.accessReviews' | hhTranslate:'Access reviews'" [subtitle]="'admin.accessReviewsSubtitle' | hhTranslate:'Certify or revoke user access on a governed schedule.'"><button mat-stroked-button type="button" (click)="load()" [disabled]="busy">{{ 'admin.refresh' | hhTranslate:'Refresh' }}</button></hh-page-header>
      <p class="notice">{{ 'admin.governanceBoundary' | hhTranslate:'Certification and separation-of-duties checks remain server-side.' }}</p><p class="error" *ngIf="error">{{ error }}</p>
      <mat-card><mat-card-header><mat-card-title>{{ 'admin.startAccessReview' | hhTranslate:'Start access review' }}</mat-card-title></mat-card-header><mat-card-content class="form-grid"><mat-form-field appearance="outline"><mat-label>{{ 'admin.subject' | hhTranslate:'Subject' }}</mat-label><mat-select [(ngModel)]="draft.subjectUserId"><mat-option *ngFor="let user of users" [value]="user.id">{{ user.email || user.userName }}</mat-option></mat-select></mat-form-field><mat-form-field appearance="outline"><mat-label>{{ 'admin.roles' | hhTranslate:'Roles to review' }}</mat-label><mat-select multiple [(ngModel)]="draft.roleIds"><mat-option *ngFor="let role of roles" [value]="role.id">{{ role.name }}</mat-option></mat-select></mat-form-field><mat-form-field appearance="outline"><mat-label>{{ 'admin.dueDays' | hhTranslate:'Due in days' }}</mat-label><input matInput type="number" min="1" max="90" [(ngModel)]="draft.dueDays"></mat-form-field><button mat-flat-button color="primary" type="button" (click)="create()" [disabled]="busy || !canWrite || !draft.subjectUserId || !draft.roleIds.length">{{ 'admin.createAccessReview' | hhTranslate:'Create review' }}</button></mat-card-content></mat-card>
      <mat-card class="table-card"><mat-card-header><mat-card-title>{{ 'admin.accessReviews' | hhTranslate:'Access reviews' }}</mat-card-title></mat-card-header><mat-card-content class="table-wrap"><table><thead><tr><th>{{ 'admin.subject' | hhTranslate:'Subject' }}</th><th>{{ 'admin.reviewer' | hhTranslate:'Reviewer' }}</th><th>{{ 'admin.status' | hhTranslate:'Status' }}</th><th>{{ 'admin.dueAt' | hhTranslate:'Due' }}</th><th>{{ 'admin.actions' | hhTranslate:'Actions' }}</th></tr></thead><tbody><tr *ngFor="let item of reviews"><td>{{ displayUser(item.subjectUserId) }}</td><td>{{ item.reviewer }}</td><td>{{ item.status }}</td><td>{{ item.dueAt | date:'short' }}</td><td><button mat-button color="primary" *ngIf="item.status === 'pending'" (click)="certify(item)" [disabled]="busy || !canWrite">{{ 'admin.certify' | hhTranslate:'Certify' }}</button><button mat-button color="warn" *ngIf="item.status === 'pending'" (click)="revoke(item)" [disabled]="busy || !canWrite">{{ 'admin.revoke' | hhTranslate:'Revoke' }}</button></td></tr><tr *ngIf="!reviews.length"><td colspan="5">{{ 'admin.noAccessReviews' | hhTranslate:'No access reviews.' }}</td></tr></tbody></table></mat-card-content></mat-card>
    </hh-page-layout>
  `,
  styles: [':host{display:block}.form-grid{display:grid;gap:var(--space-3)}.notice{padding:var(--space-3);border:1px solid var(--border-default);border-radius:var(--radius-card);background:var(--surface-muted);color:var(--text-secondary)}.error{color:var(--color-danger)}.table-card{margin-top:var(--space-4)}.table-wrap{overflow:auto}table{width:100%;border-collapse:collapse}th,td{text-align:left;padding:var(--space-2);border-bottom:1px solid var(--border-subtle);white-space:nowrap}'],
})
export class AccessReviewsPageComponent implements OnInit {
  private readonly api = inject(AdminApiService); private readonly permissions = inject(HisHopePermissionService); private readonly i18n = inject(HisHopeI18nService); private readonly snack = inject(MatSnackBar); private readonly cdr = inject(ChangeDetectorRef);
  users: User[] = []; roles: Role[] = []; reviews: AccessReview[] = []; busy = false; error = '';
  draft = { subjectUserId: '', roleIds: [] as string[], dueDays: 30 };
  get canWrite(): boolean { return this.permissions.has('admin.roles.write'); }
  ngOnInit(): void { this.load(); }
  load(): void { this.busy = true; this.error = ''; this.api.getUsers().subscribe({ next: users => { this.users = users; this.api.getRoles().subscribe({ next: roles => { this.roles = roles; this.api.getAccessReviews().subscribe({ next: reviews => { this.reviews = reviews; this.busy = false; this.cdr.markForCheck(); }, error: () => this.fail() }); }, error: () => this.fail() }); }, error: () => this.fail() }); }
  displayUser(id: string): string { const user = this.users.find(x => x.id === id); return user?.email || user?.userName || id; }
  create(): void { if (!this.canWrite) return; this.busy = true; this.api.createAccessReview(this.draft).subscribe({ next: () => { this.snack.open(this.i18n.t('admin.accessReviewCreated', 'Access review created.'), this.i18n.t('admin.close', 'Close'), { duration: 3000 }); this.draft = { subjectUserId: '', roleIds: [], dueDays: 30 }; this.load(); }, error: () => this.fail() }); }
  certify(item: AccessReview): void { this.mutate(this.api.certifyAccessReview(item.id), 'admin.accessReviewCertified', 'Access review certified.'); }
  revoke(item: AccessReview): void { this.mutate(this.api.revokeAccessReview(item.id), 'admin.accessReviewRevoked', 'Access review revoked.'); }
  private mutate(operation: import('rxjs').Observable<unknown>, key: string, fallback: string): void { if (!this.canWrite) return; this.busy = true; operation.subscribe({ next: () => { this.snack.open(this.i18n.t(key, fallback), this.i18n.t('admin.close', 'Close'), { duration: 3000 }); this.load(); }, error: () => this.fail() }); }
  private fail(): void { this.error = this.i18n.t('admin.loadAccessGovernanceFailed', 'Unable to load access review data.'); this.busy = false; this.cdr.markForCheck(); }
}
