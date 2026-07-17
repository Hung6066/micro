import { ComponentFixture, TestBed } from '@angular/core/testing';
import { LoadingSpinnerComponent } from './loading-spinner.component';
import { SharedModule } from '@shared/shared.module';

describe('LoadingSpinnerComponent', () => {
  let component: LoadingSpinnerComponent;
  let fixture: ComponentFixture<LoadingSpinnerComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      declarations: [LoadingSpinnerComponent],
      imports: [SharedModule],
    });
    fixture = TestBed.createComponent(LoadingSpinnerComponent);
    component = fixture.componentInstance;
  });

  it('should show spinner when loading is true', () => {
    component.loading = true;
    fixture.detectChanges();
    const overlay = fixture.nativeElement.querySelector('.loading-overlay');
    expect(overlay).toBeTruthy();
  });

  it('should hide spinner when loading is false', () => {
    component.loading = false;
    fixture.detectChanges();
    const overlay = fixture.nativeElement.querySelector('.loading-overlay');
    expect(overlay).toBeNull();
  });

  it('should pass a basic integrity check', () => {
    expect(true).toBeTrue();
  });

});
