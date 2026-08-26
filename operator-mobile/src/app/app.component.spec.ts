import { ComponentFixture, TestBed } from "@angular/core/testing";
import { HisHopeThemeService } from "@his-hope/frontend-foundation";
import { AppComponent } from "./app.component";

describe("AppComponent", () => {
  let fixture: ComponentFixture<AppComponent>;
  let theme: jasmine.SpyObj<HisHopeThemeService>;

  beforeEach(async () => {
    theme = jasmine.createSpyObj<HisHopeThemeService>("HisHopeThemeService", [
      "restore",
      "setPlatform",
    ]);
    await TestBed.configureTestingModule({
      imports: [AppComponent],
      providers: [{ provide: HisHopeThemeService, useValue: theme }],
    }).compileComponents();

    fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();
  });

  it("restores shared theme preferences and selects mobile density", () => {
    expect(theme.restore).toHaveBeenCalled();
    expect(theme.setPlatform).toHaveBeenCalledWith("mobile");
  });
});
