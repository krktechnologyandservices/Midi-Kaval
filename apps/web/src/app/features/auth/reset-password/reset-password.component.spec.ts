import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AuthSessionService } from '../../../core/auth/auth-session.service';
import { ResetPasswordComponent } from './reset-password.component';

describe('ResetPasswordComponent', () => {
  let fixture: ComponentFixture<ResetPasswordComponent>;
  let auth: jasmine.SpyObj<AuthSessionService>;

  beforeEach(async () => {
    auth = jasmine.createSpyObj('AuthSessionService', [
      'resetPassword',
      'extractErrorMessage',
    ]);

    await TestBed.configureTestingModule({
      imports: [ResetPasswordComponent],
      providers: [
        provideRouter([]),
        { provide: AuthSessionService, useValue: auth },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(ResetPasswordComponent);
    fixture.detectChanges();
  });

  it('renders aria-live region for feedback', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    const liveRegion = compiled.querySelector('.aria-live[aria-live="polite"]');
    expect(liveRegion).not.toBeNull();
  });
});
