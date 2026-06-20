import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AppComponent } from './app.component';
import { AuthSessionService } from './core/auth/auth-session.service';

describe('AppComponent', () => {
  beforeEach(async () => {
    const auth = {
      logout: jasmine.createSpy('logout'),
      isAuthenticated: () => false,
      currentUser: () => null,
    } as unknown as AuthSessionService;

    await TestBed.configureTestingModule({
      imports: [AppComponent],
      providers: [
        provideRouter([]),
        { provide: AuthSessionService, useValue: auth },
      ],
    }).compileComponents();
  });

  it('should render web shell outlet', () => {
    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('main.shell router-outlet, main.shell')).not.toBeNull();
  });
});
