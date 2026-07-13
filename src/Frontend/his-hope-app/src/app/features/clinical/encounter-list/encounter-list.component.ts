import { Component } from '@angular/core';

@Component({
  selector: 'app-encounter-list',
  template: `
    <div class="encounters">
      <div class="header">
        <h1>Clinical Encounters</h1>
        <button mat-raised-button color="primary">
          <mat-icon>add</mat-icon> New Encounter
        </button>
      </div>
      <mat-card>
        <mat-card-content>
          <p class="placeholder">Patient encounter list and SOAP notes will be displayed here.</p>
        </mat-card-content>
      </mat-card>
    </div>
  `,
  styles: [`
    .encounters { padding: 24px; }
    .header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 24px; }
    .placeholder { color: #999; text-align: center; padding: 48px; }
  `],
})
export class EncounterListComponent {}
