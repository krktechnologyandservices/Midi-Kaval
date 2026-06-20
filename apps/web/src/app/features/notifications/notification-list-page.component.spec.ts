import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { CaseApiService } from '../cases/services/case-api.service';
import { NotificationDeeplinkService } from './notification-deeplink.service';
import { NotificationListPageComponent } from './notification-list-page.component';

describe('NotificationListPageComponent', () => {
  let fixture: ComponentFixture<NotificationListPageComponent>;
  let markReadSpy: jasmine.Spy;
  let navigateSpy: jasmine.Spy;

  const mockNotifications = [
    {
      id: 'n1',
      title: 'Test notification',
      body: 'This is a test body',
      createdAtUtc: '2026-06-20T10:00:00Z',
      readAtUtc: null,
      isRead: false,
      eventType: 'test.event',
      caseId: 'case-1',
      resourceType: 'CourtSitting',
      resourceId: 'res-1',
    },
    {
      id: 'n2',
      title: 'Read notification',
      body: 'Already read',
      createdAtUtc: '2026-06-19T10:00:00Z',
      readAtUtc: '2026-06-19T12:00:00Z',
      isRead: true,
      eventType: 'test.event2',
      caseId: null,
      resourceType: null,
      resourceId: null,
    },
  ];

  beforeEach(async () => {
    markReadSpy = jasmine.createSpy('markNotificationRead').and.returnValue(of({}));
    navigateSpy = jasmine.createSpy('navigate');

    await TestBed.configureTestingModule({
      imports: [NotificationListPageComponent],
      providers: [
        provideRouter([]),
        {
          provide: CaseApiService,
          useValue: {
            listNotifications: () => of({ items: mockNotifications }),
            markNotificationRead: markReadSpy,
          },
        },
        {
          provide: NotificationDeeplinkService,
          useValue: { navigate: navigateSpy },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(NotificationListPageComponent);
    fixture.detectChanges();
  });

  it('renders notification rows', async () => {
    await fixture.whenStable();
    fixture.detectChanges();

    const items = fixture.nativeElement.querySelectorAll('.notification-item');
    expect(items.length).toBe(2);
    expect(items[0].textContent).toContain('Test notification');
    expect(items[1].textContent).toContain('Read notification');
  });

  it('marks unread notification as read on click', async () => {
    await fixture.whenStable();
    fixture.detectChanges();

    const items = fixture.nativeElement.querySelectorAll('.notification-item');
    items[0].click();

    expect(markReadSpy).toHaveBeenCalledWith('n1');
    expect(navigateSpy).toHaveBeenCalled();
  });

  it('does not mark already-read notification on click', async () => {
    await fixture.whenStable();
    fixture.detectChanges();

    const items = fixture.nativeElement.querySelectorAll('.notification-item');
    items[1].click();

    expect(markReadSpy).not.toHaveBeenCalled();
    expect(navigateSpy).toHaveBeenCalled();
  });

  it('shows empty state when no notifications', async () => {
    TestBed.resetTestingModule();
    await TestBed.configureTestingModule({
      imports: [NotificationListPageComponent],
      providers: [
        provideRouter([]),
        {
          provide: CaseApiService,
          useValue: {
            listNotifications: () => of({ items: [] }),
            markNotificationRead: markReadSpy,
          },
        },
        {
          provide: NotificationDeeplinkService,
          useValue: { navigate: navigateSpy },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(NotificationListPageComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain("You're up to date");
  });

  it('handles error state gracefully', async () => {
    TestBed.resetTestingModule();
    await TestBed.configureTestingModule({
      imports: [NotificationListPageComponent],
      providers: [
        provideRouter([]),
        {
          provide: CaseApiService,
          useValue: {
            listNotifications: () => throwError(() => new Error('Network error')),
            markNotificationRead: markReadSpy,
          },
        },
        {
          provide: NotificationDeeplinkService,
          useValue: { navigate: navigateSpy },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(NotificationListPageComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    // Should not throw and should stop loading
    expect(fixture.nativeElement.textContent).not.toContain('Loading');
  });
});
