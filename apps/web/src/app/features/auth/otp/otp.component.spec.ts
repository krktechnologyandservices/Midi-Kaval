import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { signal } from '@angular/core';
import { AuthSessionService } from '../../../core/auth/auth-session.service';
import { OtpComponent } from './otp.component';

describe('OtpComponent', () => {
  let fixture: ComponentFixture<OtpComponent>;
  let auth: jasmine.SpyObj<AuthSessionService>;

  beforeEach(async () => {
    auth = jasmine.createSpyObj('AuthSessionService', [
      'verifyOtp',
      'extractErrorMessage',
      'navigateAfterLogin',
    ], {
      otpChallenge: signal({
        challengeId: '11111111-1111-4111-8111-111111111111',
        expiresInSeconds: 2,
      }),
    });

    await TestBed.configureTestingModule({
      imports: [OtpComponent],
      providers: [
        provideRouter([]),
        { provide: AuthSessionService, useValue: auth },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(OtpComponent);
    fixture.detectChanges();
  });

  it('renders aria-live region for OTP feedback', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    const liveRegion = compiled.querySelector('.aria-live[aria-live="polite"]');
    expect(liveRegion).not.toBeNull();
  });
});
