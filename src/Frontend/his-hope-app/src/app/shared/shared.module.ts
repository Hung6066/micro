import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatListModule } from '@angular/material/list';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule, MatRippleModule } from '@angular/material/core';
import { MatDialogModule } from '@angular/material/dialog';
import { MatSnackBarModule } from '@angular/material/snack-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatPaginatorModule } from '@angular/material/paginator';
import { MatSortModule } from '@angular/material/sort';
import { MatMenuModule } from '@angular/material/menu';
import { MatBadgeModule } from '@angular/material/badge';
import { MatChipsModule } from '@angular/material/chips';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatRadioModule } from '@angular/material/radio';
import { MatTabsModule } from '@angular/material/tabs';
import { SidebarComponent } from './components/sidebar/sidebar.component';

const materialModules = [
  MatToolbarModule, MatSidenavModule, MatListModule, MatButtonModule,
  MatIconModule, MatCardModule, MatTableModule, MatFormFieldModule,
  MatInputModule, MatSelectModule, MatDatepickerModule, MatNativeDateModule,
  MatDialogModule, MatSnackBarModule, MatProgressSpinnerModule,
  MatPaginatorModule, MatSortModule, MatMenuModule, MatBadgeModule,
  MatChipsModule, MatTooltipModule, MatCheckboxModule, MatRadioModule,
  MatTabsModule, MatRippleModule,
];

@NgModule({
  declarations: [SidebarComponent],
  imports: [CommonModule, RouterModule, ...materialModules],
  exports: [
    CommonModule, ReactiveFormsModule, FormsModule,
    ...materialModules, SidebarComponent,
  ],
})
export class SharedModule {}
