import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AuthSessionService } from '../../../core/auth/auth-session.service';
import { ForgotPasswordComponent } from './forgot-password.component';

describe('ForgotPasswordComponent', () => {
  let fixture: ComponentFixture<ForgotPasswordComponent>;
  let auth: jasmine.SpyObj<AuthSessionService>;

  beforeEach(async () => {
    auth = jasmine.createSpyObj<AuthSessionService>('AuthSessionService', [
      'forgotPassword',
      'extractErrorMessage',
    ]);

    await TestBed.configureTestingModule({
      imports: [ForgotPasswordComponent],
      providers: [
        provideRouter([]),
        { provide: AuthSessionService, useValue: auth },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(ForgotPasswordComponent);
    fixture.detectChanges();
  });

  it('renders aria-live region for feedback', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    const liveRegion = compiled.querySelector('.aria-live[aria-live="polite"]');
    expect(liveRegion).not.toBeNull();
  });
});
