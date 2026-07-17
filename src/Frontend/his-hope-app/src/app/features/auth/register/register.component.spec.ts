import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { RegisterComponent } from './register.component';
import { SharedModule } from '@shared/shared.module';

describe('RegisterComponent', () => {
  let component: RegisterComponent;
  let fixture: ComponentFixture<RegisterComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      declarations: [RegisterComponent],
      imports: [SharedModule, NoopAnimationsModule],
    });
    fixture = TestBed.createComponent(RegisterComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should display register heading', () => {
    const heading: HTMLElement = fixture.nativeElement.querySelector('h1');
    expect(heading?.textContent?.trim()).toBe('Register');
  });

  it('should have a link back to login', () => {
    const link: HTMLAnchorElement = fixture.nativeElement.querySelector('button[routerLink="/auth/login"]');
    expect(link).toBeTruthy();
  });

  it('should have component initialized', () => {
    expect(component).toBeDefined();
  });

  it('should have fixture defined', () => {
    expect(fixture).toBeDefined();
  });
});
