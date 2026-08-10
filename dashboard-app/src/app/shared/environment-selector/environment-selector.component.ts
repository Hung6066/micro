import { Component, Output, EventEmitter, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatSelectModule } from '@angular/material/select';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { Observable, of } from 'rxjs';
import { map } from 'rxjs/operators';
import { EnvironmentService } from '../../core/services/environment.service';

const ENVIRONMENTS = [
  { value: 'Development', label: 'Development' },
  { value: 'Staging', label: 'Staging' },
  { value: 'Production', label: 'Production' },
];

@Component({
  selector: 'app-environment-selector',
  standalone: true,
  imports: [
    CommonModule,
    MatSelectModule,
    MatFormFieldModule,
    MatIconModule,
  ],
  template: `
    <mat-form-field appearance="outline" subscriptSizing="dynamic" class="env-selector">
      <mat-icon matPrefix>cloud</mat-icon>
      <mat-select
        [value]="currentEnv$ | async"
        (selectionChange)="onChange($event.value)"
        [disabled]="(loading$ | async) ?? false">
        <mat-option *ngFor="let env of environments" [value]="env.value">
          {{ env.label }}
        </mat-option>
      </mat-select>
    </mat-form-field>
  `,
  styles: [`
    .env-selector {
      width: 180px;
      margin: 0;
    }
    .env-selector ::ng-deep .mat-mdc-form-field-subscript-wrapper {
      display: none;
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EnvironmentSelectorComponent {
  @Output() environmentChange = new EventEmitter<string>();

  readonly environments = ENVIRONMENTS;
  readonly currentEnv$: Observable<string>;
  readonly loading$: Observable<boolean>;

  constructor(private readonly envService: EnvironmentService) {
    this.currentEnv$ = this.envService.getCurrent().pipe(
      map(ctx => ctx.name),
    );
    this.loading$ = of(false);
  }

  onChange(value: string): void {
    this.environmentChange.emit(value);
  }
}
