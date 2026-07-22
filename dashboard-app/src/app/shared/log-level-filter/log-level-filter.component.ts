import { Component, Input, Output, EventEmitter, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatChipsModule } from '@angular/material/chips';
import { MatIconModule } from '@angular/material/icon';

export interface LevelOption {
  value: string;
  label: string;
  color: string;
  bg: string;
}

const LEVELS: LevelOption[] = [
  { value: '',       label: 'Tất cả',    color: '#787774', bg: '#F0F0EE' },
  { value: 'Error',  label: 'Error',    color: '#C25450', bg: '#FDEBEC' },
  { value: 'Warning', label: 'Warning', color: '#B6581C', bg: '#FDF0E2' },
  { value: 'Information', label: 'Info', color: '#2563EB', bg: '#E1F3FE' },
  { value: 'Debug',  label: 'Debug',    color: '#6B4FA0', bg: '#F3EDF8' },
];

@Component({
  selector: 'app-log-level-filter',
  standalone: true,
  imports: [CommonModule, MatChipsModule, MatIconModule],
  template: `
    <mat-chip-listbox
      [value]="selected"
      (change)="onLevelChange($event.value)"
      class="level-filter"
      selectable="true"
      multiple="false">
      <mat-chip-option
        *ngFor="let level of levels"
        [value]="level.value"
        [selected]="selected === level.value"
        [style.--chip-color]="level.color"
        [style.--chip-bg]="level.bg">
        {{ level.label }}
      </mat-chip-option>
    </mat-chip-listbox>
  `,
  styles: [`
    .level-filter {
      display: flex;
      gap: 4px;
    }
    .mat-mdc-chip {
      --mdc-chip-label-text-color: var(--chip-color, #787774);
      --mdc-chip-outline-color: var(--chip-bg, #F0F0EE);
      --mdc-chip-hover-outline-color: var(--chip-color);
      --mdc-chip-selected-outline-color: var(--chip-color);
      --mdc-chip-elevated-container-color: transparent;
      --mdc-chip-selected-container-color: var(--chip-bg, #F0F0EE);
      --mdc-chip-flat-selected-outline-color: var(--chip-color);
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LogLevelFilterComponent {
  @Input() selected = '';
  @Output() levelChange = new EventEmitter<string>();

  readonly levels = LEVELS;

  onLevelChange(value: string): void {
    this.levelChange.emit(value);
  }
}
