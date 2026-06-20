import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { HandoffWhisperComponent } from './handoff-whisper.component';
import { HandoffWhisperDto } from '../models/case.models';

describe('HandoffWhisperComponent', () => {
  let fixture: ComponentFixture<HandoffWhisperComponent>;

  const whisper: HandoffWhisperDto = {
    priorActions: 'Home visit completed',
    openItems: 'School enrollment pending',
    nextVisitPurpose: 'Follow up housing',
    transferredAtUtc: '2026-06-15T10:00:00Z',
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [HandoffWhisperComponent],
      providers: [provideRouter([])],
    }).compileComponents();

    fixture = TestBed.createComponent(HandoffWhisperComponent);
    fixture.componentRef.setInput('whisper', whisper);
    fixture.detectChanges();
  });

  it('renders three labeled whisper lines', () => {
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Prior actions:');
    expect(text).toContain('Open items:');
    expect(text).toContain('Next visit:');
    expect(text).toContain(whisper.priorActions);
    expect(text).toContain(whisper.openItems);
    expect(text).toContain(whisper.nextVisitPurpose);
  });

  it('emits viewFullTimeline when link clicked', () => {
    const emitted: boolean[] = [];
    fixture.componentInstance.viewFullTimeline.subscribe(() => emitted.push(true));

    const button = fixture.nativeElement.querySelector('.timeline-link') as HTMLButtonElement;
    button.click();

    expect(emitted.length).toBe(1);
  });
});
