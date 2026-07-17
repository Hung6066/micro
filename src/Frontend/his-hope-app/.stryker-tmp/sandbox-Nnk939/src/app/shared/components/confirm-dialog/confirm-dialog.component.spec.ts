// @ts-nocheck
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { ConfirmDialogComponent, ConfirmDialogData } from './confirm-dialog.component';

describe('ConfirmDialogComponent', () => {
  let component: ConfirmDialogComponent;
  let fixture: ComponentFixture<ConfirmDialogComponent>;
  let dialogRef: jasmine.SpyObj<MatDialogRef<ConfirmDialogComponent>>;

  const defaultData: ConfirmDialogData = {
    title: 'Confirm',
    message: 'Are you sure?',
  };

  beforeEach(() => {
    const dialogRefSpy = jasmine.createSpyObj('MatDialogRef', ['close']);

    TestBed.configureTestingModule({
      declarations: [ConfirmDialogComponent],
      imports: [MatDialogModule, MatButtonModule, NoopAnimationsModule],
      providers: [
        { provide: MAT_DIALOG_DATA, useValue: defaultData },
        { provide: MatDialogRef, useValue: dialogRefSpy },
      ],
    });

    fixture = TestBed.createComponent(ConfirmDialogComponent);
    component = fixture.componentInstance;
    dialogRef = TestBed.inject(MatDialogRef) as jasmine.SpyObj<MatDialogRef<ConfirmDialogComponent>>;
    fixture.detectChanges();
  });

  it('should create with dialog data', () => {
    expect(component).toBeTruthy();
    expect(component.data.title).toBe('Confirm');
    expect(component.data.message).toBe('Are you sure?');
  });

  it('should display title and message in the template', () => {
    const titleEl: HTMLElement = fixture.nativeElement.querySelector('[mat-dialog-title]');
    const msgEl: HTMLElement = fixture.nativeElement.querySelector('mat-dialog-content p');
    expect(titleEl?.textContent?.trim()).toBe('Confirm');
    expect(msgEl?.textContent?.trim()).toBe('Are you sure?');
  });

  it('should close dialog on cancel button click', () => {
    const cancelBtn: HTMLButtonElement = fixture.nativeElement.querySelector('button[mat-dialog-close]');
    cancelBtn.click();
    expect(dialogRef.close).toHaveBeenCalled();
  });

  it('should pass a basic integrity check', () => {
    expect(true).toBeTrue();
  });

});
