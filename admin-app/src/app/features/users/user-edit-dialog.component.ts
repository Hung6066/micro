import { Component, Inject, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { AdminApiService, User } from '../../core/services/admin-api.service';
import { HisHopeCreateDialogShellComponent, HisHopeFormLayoutComponent, HisHopeFormSectionComponent, HisHopeI18nService, HisHopeTranslatePipe } from '@his-hope/frontend-foundation';
import { catchError, of } from 'rxjs';

@Component({
  selector: 'app-user-edit-dialog', standalone: true,
  imports: [CommonModule, FormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatButtonModule, MatSelectModule, MatSnackBarModule, HisHopeCreateDialogShellComponent, HisHopeFormLayoutComponent, HisHopeFormSectionComponent, HisHopeTranslatePipe],
  template: `<form #formRef="ngForm" (ngSubmit)="save()"><hh-create-dialog-shell [title]="(isEdit ? 'admin.editUser' : 'admin.createUser') | hhTranslate"><div hhCreateDialogContent><hh-form-layout><hh-form-section [title]="'admin.basicInformation' | hhTranslate" [span]="2"><div class="fields"><mat-form-field appearance="outline"><mat-label>{{ 'admin.username' | hhTranslate }}</mat-label><input matInput name="username" [(ngModel)]="form.username" [disabled]="isEdit" required></mat-form-field><mat-form-field appearance="outline"><mat-label>{{ 'admin.email' | hhTranslate }}</mat-label><input matInput type="email" name="email" [(ngModel)]="form.email" required></mat-form-field><mat-form-field appearance="outline"><mat-label>{{ 'admin.firstName' | hhTranslate:'First name' }}</mat-label><input matInput name="firstName" [(ngModel)]="form.firstName" required></mat-form-field><mat-form-field appearance="outline"><mat-label>{{ 'admin.lastName' | hhTranslate:'Last name' }}</mat-label><input matInput name="lastName" [(ngModel)]="form.lastName" required></mat-form-field><mat-form-field appearance="outline"><mat-label>{{ 'admin.phoneNumber' | hhTranslate:'Phone number' }}</mat-label><input matInput name="phoneNumber" [(ngModel)]="form.phoneNumber"></mat-form-field><mat-form-field appearance="outline" *ngIf="!isEdit"><mat-label>{{ 'admin.password' | hhTranslate }}</mat-label><input matInput type="password" name="password" [(ngModel)]="form.password" required minlength="12"></mat-form-field><mat-form-field appearance="outline"><mat-label>{{ 'admin.role' | hhTranslate }}</mat-label><input matInput name="role" [(ngModel)]="form.role"></mat-form-field></div></hh-form-section></hh-form-layout></div><div hhCreateDialogFooter><button mat-button type="button" (click)="dialogRef.close()">{{ 'admin.cancel' | hhTranslate }}</button><button mat-flat-button color="primary" type="submit" [disabled]="formRef.invalid || saving">{{ saving ? ('admin.saving' | hhTranslate) : ('admin.save' | hhTranslate) }}</button></div></hh-create-dialog-shell></form>`,
  styles: [`.fields{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:var(--form-field-gap)}mat-form-field{width:100%}@media(max-width:720px){.fields{grid-template-columns:1fr}}`],
})
export class UserEditDialogComponent {
  readonly dialogRef = inject(MatDialogRef<UserEditDialogComponent>); private readonly api = inject(AdminApiService); private readonly snack = inject(MatSnackBar); private readonly i18n = inject(HisHopeI18nService);
  isEdit = false; saving = false; form: any = { username: '', email: '', password: '', firstName: '', lastName: '', phoneNumber: '', role: '' };
  constructor(@Inject(MAT_DIALOG_DATA) data: User | null) { this.isEdit = !!data; if (data) this.form = { ...this.form, ...data, username: data.userName }; }
  save(): void { this.saving = true; const request = this.isEdit ? this.api.updateUser(this.form.id, { firstName: this.form.firstName, lastName: this.form.lastName, email: this.form.email, phoneNumber: this.form.phoneNumber, role: this.form.role, concurrencyToken: this.form.concurrencyToken }) : this.api.createUser(this.form); request.pipe(catchError(() => { this.snack.open(this.i18n.t('admin.saveUserFailed', 'Failed to save user'), this.i18n.t('admin.close', 'Close'), { duration: 3000 }); this.saving = false; return of(null); })).subscribe(result => { if (result) this.dialogRef.close(true); }); }
}
