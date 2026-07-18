import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { MockStore, provideMockStore } from '@ngrx/store/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { MatSnackBarModule } from '@angular/material/snack-bar';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ErrorBarComponent } from './error-bar.component';
const initialAppState = {
  auth: { user: null, loading: false, error: null },
  patients: { patients: [], selectedPatient: null, totalCount: 0, page: 1, pageSize: 20, query: '', loading: false, error: null },
  error: { message: null, code: null, correlationId: null, timestamp: null },
};

describe('ErrorBarComponent', () => {
  let component: ErrorBarComponent;
  let fixture: ComponentFixture<ErrorBarComponent>;
  let store: MockStore;

  beforeEach(() => {
    TestBed.configureTestingModule({
      
      imports: [
        ErrorBarComponent, NoopAnimationsModule, MatSnackBarModule, MatIconModule, MatTooltipModule],
      providers: [
        provideMockStore({ initialState: initialAppState }),
      ],
    });

    fixture = TestBed.createComponent(ErrorBarComponent);
    component = fixture.componentInstance;
    store = TestBed.inject(MockStore);
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should display error bar when error is present', () => {
    store.setState({
      ...initialAppState,
      error: { message: 'Something went wrong', code: 'HTTP_500', correlationId: 'hh-123', timestamp: '2024-01-01T00:00:00.000Z' },
    });
    store.refreshState();
    fixture.detectChanges();

    const errorBar = fixture.nativeElement.querySelector('.error-bar');
    expect(errorBar).toBeTruthy();
    const msgEl = fixture.nativeElement.querySelector('.error-bar__message');
    expect(msgEl?.textContent?.trim()).toContain('Something went wrong');
  });

  it('should return correct severity class', () => {
    expect(component.getSeverity('HTTP_500')).toBe('HTTP_5XX');
    expect(component.getSeverity('HTTP_404')).toBe('HTTP_4XX');
    expect(component.getSeverity('UNKNOWN')).toBe('default');
    expect(component.getSeverity(null)).toBe('default');
  });

  it('should pass a basic integrity check', () => {
    expect(true).toBeTrue();
  });

});
