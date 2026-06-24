import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { ActivatedRoute } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { ActivationComponent } from './activation.component';
import { ActivationApiService } from './activation-api.service';

describe('ActivationComponent', () => {
  let fixture: ComponentFixture<ActivationComponent>;
  let api: jasmine.SpyObj<ActivationApiService>;

  function createRoute(token: string | null, sig: string | null) {
    return {
      snapshot: {
        queryParamMap: {
          get(key: string): string | null {
            if (key === 'token') return token;
            if (key === 'sig') return sig;
            return null;
          },
        },
      },
    } as unknown as ActivatedRoute;
  }

  describe('with valid query params', () => {
    beforeEach(async () => {
      api = jasmine.createSpyObj('ActivationApiService', [
        'validateLink',
        'activateOrganisation',
      ]);
      api.validateLink.and.resolveTo({ email: 'director@example.org', organisationName: 'Test Org' });

      await TestBed.configureTestingModule({
        imports: [ActivationComponent],
        providers: [
          provideRouter([]),
          { provide: ActivatedRoute, useFactory: () => createRoute('valid-token', 'valid-sig') },
          { provide: ActivationApiService, useValue: api },
        ],
      }).compileComponents();

      fixture = TestBed.createComponent(ActivationComponent);
      fixture.detectChanges();
    });

    it('validates the link on init and shows the info step', async () => {
      expect(api.validateLink).toHaveBeenCalledWith('valid-token', 'valid-sig');
      await fixture.whenStable();
      fixture.detectChanges();
      const component = fixture.componentInstance;
      expect(component.step()).toBe('info');
      const compiled = fixture.nativeElement as HTMLElement;
      expect(compiled.querySelector('h1')?.textContent).toContain('Welcome to Kaval');
      expect(compiled.querySelector('button')?.textContent).toContain('Continue to Setup');
    });

    it('shows the pre-filled email as read-only on the info step', async () => {
      await fixture.whenStable();
      fixture.detectChanges();
      const compiled = fixture.nativeElement as HTMLElement;
      const emailInput = compiled.querySelector('#email') as HTMLInputElement;
      expect(emailInput).not.toBeNull();
      expect(emailInput.value).toBe('director@example.org');
    });

    it('proceeds to the form step when Continue is clicked', async () => {
      await fixture.whenStable();
      fixture.detectChanges();

      const component = fixture.componentInstance;
      component.proceedToForm();
      fixture.detectChanges();

      expect(component.step()).toBe('form');
      const compiled = fixture.nativeElement as HTMLElement;
      expect(compiled.querySelector('button')?.textContent).toContain('Activate Account');
    });

    it('requires full name and password', () => {
      const component = fixture.componentInstance;
      component.proceedToForm();
      component.form.controls.fullName.setValue('');
      component.form.controls.password.setValue('');
      expect(component.form.invalid).toBeTrue();
    });

    it('validates password policy', () => {
      const component = fixture.componentInstance;
      component.proceedToForm();
      component.form.controls.password.setValue('weak');
      expect(component.form.controls.password.errors).not.toBeNull();
    });

    it('submits and shows success page', async () => {
      api.activateOrganisation.and.resolveTo({
        userId: 'abc-123',
        organisationId: 'org-456',
        organisationName: 'Test Org',
        message: 'Your organisation is active. Welcome to Kaval.',
      });

      const component = fixture.componentInstance;
      component.proceedToForm();
      component.form.controls.fullName.setValue('Jane Smith');
      component.form.controls.password.setValue('StrongPass1');
      await component.submit();

      expect(component.step()).toBe('success');
      expect(component.successMessage()).toContain('Welcome to Kaval');
    });

    it('shows error when activation fails', async () => {
      api.activateOrganisation.and.rejectWith(
        new HttpErrorResponse({ status: 400, error: { detail: 'Link expired.' } }),
      );

      const component = fixture.componentInstance;
      component.proceedToForm();
      component.form.controls.fullName.setValue('Jane Smith');
      component.form.controls.password.setValue('StrongPass1');
      await component.submit();

      expect(component.errorMessage()).toContain('Link expired.');
      expect(component.step()).toBe('form'); // stays on form page
    });
  });

  describe('with missing query params', () => {
    beforeEach(async () => {
      api = jasmine.createSpyObj('ActivationApiService', ['validateLink']);

      await TestBed.configureTestingModule({
        imports: [ActivationComponent],
        providers: [
          provideRouter([]),
          { provide: ActivatedRoute, useFactory: () => createRoute(null, null) },
          { provide: ActivationApiService, useValue: api },
        ],
      }).compileComponents();

      fixture = TestBed.createComponent(ActivationComponent);
      fixture.detectChanges();
    });

    it('shows error for invalid link', () => {
      const compiled = fixture.nativeElement as HTMLElement;
      expect(compiled.querySelector('h2')?.textContent).toContain('Activation Link Issue');
    });
  });

  describe('with expired or invalid link', () => {
    beforeEach(async () => {
      api = jasmine.createSpyObj('ActivationApiService', [
        'validateLink',
        'activateOrganisation',
      ]);
      api.validateLink.and.rejectWith(
        new HttpErrorResponse({
          status: 400,
          error: { detail: 'This link has expired or already been used.' },
        }),
      );

      await TestBed.configureTestingModule({
        imports: [ActivationComponent],
        providers: [
          provideRouter([]),
          { provide: ActivatedRoute, useFactory: () => createRoute('some-token', 'some-sig') },
          { provide: ActivationApiService, useValue: api },
        ],
      }).compileComponents();

      fixture = TestBed.createComponent(ActivationComponent);
      fixture.detectChanges();
    });

    it('displays expired link message', async () => {
      await fixture.whenStable();
      fixture.detectChanges();
      const compiled = fixture.nativeElement as HTMLElement;
      expect(compiled.querySelector('h2')?.textContent).toContain('Activation Link Issue');
    });
  });
});
