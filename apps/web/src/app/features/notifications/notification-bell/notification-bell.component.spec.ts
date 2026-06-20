import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { CaseApiService } from '../../cases/services/case-api.service';
import { NotificationBellComponent } from './notification-bell.component';

describe('NotificationBellComponent', () => {
  let fixture: ComponentFixture<NotificationBellComponent>;
  let getUnreadCountSpy: jasmine.Spy;

  beforeEach(async () => {
    getUnreadCountSpy = jasmine.createSpy('getUnreadCount').and.returnValue(of(5));

    await TestBed.configureTestingModule({
      imports: [NotificationBellComponent],
      providers: [
        provideRouter([]),
        { provide: CaseApiService, useValue: { getUnreadCount: getUnreadCountSpy } },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(NotificationBellComponent);
    fixture.detectChanges();
  });

  it('renders the bell icon', () => {
    const icon = fixture.nativeElement.querySelector('mat-icon');
    expect(icon).toBeTruthy();
    expect(icon.textContent).toContain('notifications');
  });

  it('shows badge with unread count', () => {
    const link = fixture.nativeElement.querySelector('a');
    expect(link.getAttribute('ng-reflect-mat-badge')).toBe('5');
  });

  it('hides badge when count is zero', async () => {
    getUnreadCountSpy.and.returnValue(of(0));
    fixture = TestBed.createComponent(NotificationBellComponent);
    fixture.detectChanges();
    // wait for the async pipe
    await fixture.whenStable();
    fixture.detectChanges();

    const link = fixture.nativeElement.querySelector('a');
    expect(link.getAttribute('ng-reflect-mat-badge-hidden')).toBe('true');
  });

  it('navigates to /notifications on click', () => {
    const link = fixture.nativeElement.querySelector('a');
    expect(link.getAttribute('ng-reflect-router-link')).toBe('/notifications');
  });
});
