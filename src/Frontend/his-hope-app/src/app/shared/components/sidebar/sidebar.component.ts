import {
  Component,
  OnInit,
  OnDestroy,
  Input,
  Output,
  EventEmitter,
  ChangeDetectionStrategy,
  inject,
  ChangeDetectorRef,
} from "@angular/core";
import { Router, RouterModule } from "@angular/router";
import { FormControl, ReactiveFormsModule } from "@angular/forms";
import { Subject, takeUntil, debounceTime, distinctUntilChanged } from "rxjs";
import { CommonModule } from "@angular/common";
import { MatListModule } from "@angular/material/list";
import { MatIconModule } from "@angular/material/icon";
import { MatBadgeModule } from "@angular/material/badge";
import { MatTooltipModule } from "@angular/material/tooltip";
import { MatButtonModule } from "@angular/material/button";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatInputModule } from "@angular/material/input";
import { MatAutocompleteModule } from "@angular/material/autocomplete";
import { AuthService } from "@core/services/auth.service";
import { PatientService } from "@core/services/patient.service";
import { User } from "@core/models/auth.model";
import { Patient } from "@core/models/patient.model";
import { HisHopeBrandComponent } from "@his-hope/frontend-foundation/ui";
import { HisHopeTranslatePipe } from "@his-hope/frontend-foundation/i18n";

@Component({
  selector: "app-sidebar",
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    ReactiveFormsModule,
    MatListModule,
    MatIconModule,
    MatBadgeModule,
    MatTooltipModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatAutocompleteModule,
    HisHopeBrandComponent,
    HisHopeTranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: "./sidebar.component.html",
  styleUrls: ["./sidebar.component.scss"],
})
export class SidebarComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();
  currentUser: User | null = null;
  loggingOut = false;

  // Patient search
  patientSearchControl = new FormControl("");
  searchResults: Patient[] = [];

  @Input() sidenavOpened = true;
  @Output() toggle = new EventEmitter<void>();

  private authService = inject(AuthService);
  private patientService = inject(PatientService);
  private router = inject(Router);
  private cdr = inject(ChangeDetectorRef);

  ngOnInit(): void {
    this.authService.currentUser$
      .pipe(takeUntil(this.destroy$))
      .subscribe((user) => {
        this.currentUser = user;
        this.cdr.detectChanges();
      });

    this.patientSearchControl.valueChanges
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntil(this.destroy$))
      .subscribe((term) => {
        const query = (term ?? "").trim();
        if (query.length < 2) {
          this.searchResults = [];
          this.cdr.detectChanges();
          return;
        }
        this.patientService
          .search(query, 1, 10)
          .pipe(takeUntil(this.destroy$))
          .subscribe((res) => {
            this.searchResults = res.items;
            this.cdr.detectChanges();
          });
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  displayPatientName(patient: Patient): string {
    return patient ? patient.fullName : "";
  }

  onPatientSelected(event: any): void {
    const patient: Patient = event.option.value;
    if (patient) {
      this.patientSearchControl.setValue("", { emitEvent: false });
      this.searchResults = [];
      this.router.navigate(["/patients", patient.id, "workspace"]);
      this.cdr.detectChanges();
    }
  }

  logout(): void {
    this.loggingOut = true;
    this.authService
      .logout()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        complete: () => {
          this.loggingOut = false;
          this.cdr.detectChanges();
        },
        error: () => {
          this.loggingOut = false;
          this.cdr.detectChanges();
        },
      });
  }
}
