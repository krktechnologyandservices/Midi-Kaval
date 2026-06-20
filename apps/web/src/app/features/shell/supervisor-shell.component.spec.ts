import { ComponentFixture, TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { provideRouter } from '@angular/router';
import { AppRole } from '@midi-kaval/shared-types';
import { AuthSessionService } from '../../core/auth/auth-session.service';
import { SupervisorShellComponent } from './supervisor-shell.component';

describe('SupervisorShellComponent', () => {
  let fixture: ComponentFixture<SupervisorShellComponent>;
  let userSignal = signal<{ role: AppRole } | null>(null);

  it('shows Admin link only for Director', async () => {
    const authStub = {
      currentUser: userSignal,
    } as unknown as AuthSessionService;

    await TestBed.configureTestingModule({
      imports: [SupervisorShellComponent],
      providers: [
        provideRouter([]),
        { provide: AuthSessionService, useValue: authStub },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(SupervisorShellComponent);

    userSignal.set({ role: AppRole.Coordinator });
    fixture.detectChanges();
    const coordinatorText = fixture.nativeElement.textContent as string;
    expect(coordinatorText).toContain('Crisis queue');
    expect(coordinatorText).toContain('Cases');
    expect(coordinatorText).not.toContain('Admin');

    userSignal.set({ role: AppRole.Director });
    fixture.detectChanges();
    const directorText = fixture.nativeElement.textContent as string;
    expect(directorText).toContain('Admin');
  });
});

