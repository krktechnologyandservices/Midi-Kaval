import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AuthSessionService } from '../../../core/auth/auth-session.service';
import { LoginComponent } from './login.component';

describe('LoginComponent', () => {
  let fixture: ComponentFixture<LoginComponent>;
  let auth: jasmine.SpyObj<AuthSessionService>;

  beforeEach(async () => {
    auth = jasmine.createSpyObj<AuthSessionService>('AuthSessionService', [
      'login',
      'extractErrorMessage',
    ]);

    await TestBed.configureTestingModule({
      imports: [LoginComponent],
      providers: [
        provideRouter([]),
        { provide: AuthSessionService, useValue: auth },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(LoginComponent);
    fixture.detectChanges();
  });

  it('renders aria-live region for errors', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    const liveRegion = compiled.querySelector('.aria-live[aria-live="polite"]');
    expect(liveRegion).not.toBeNull();
  });
});
