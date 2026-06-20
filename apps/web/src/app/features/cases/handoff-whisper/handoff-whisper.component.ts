import { Component, input, output } from '@angular/core';
import { HandoffWhisperDto } from '../models/case.models';

@Component({
  selector: 'app-handoff-whisper',
  templateUrl: './handoff-whisper.component.html',
  styleUrl: './handoff-whisper.component.scss',
})
export class HandoffWhisperComponent {
  readonly whisper = input.required<HandoffWhisperDto>();
  readonly viewFullTimeline = output<void>();

  onViewFullTimeline(): void {
    this.viewFullTimeline.emit();
  }
}
