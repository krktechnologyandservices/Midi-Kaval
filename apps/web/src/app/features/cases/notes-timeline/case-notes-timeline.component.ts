import {
  Component,
  ElementRef,
  inject,
  input,
  OnInit,
  signal,
  viewChild,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import {
  ALLOWED_ATTACHMENT_CONTENT_TYPES,
  attachmentBasename,
  CASE_NOTE_TYPES,
  CaseNoteDto,
  CaseNoteType,
  CreateCaseNoteRequest,
  MAX_ATTACHMENT_BYTES,
} from '../models/case.models';
import { AttachmentApiService } from '../services/attachment-api.service';
import { CaseApiService } from '../services/case-api.service';

@Component({
  selector: 'app-case-notes-timeline',
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
  ],
  templateUrl: './case-notes-timeline.component.html',
  styleUrl: './case-notes-timeline.component.scss',
})
export class CaseNotesTimelineComponent implements OnInit {
  private readonly caseApi = inject(CaseApiService);
  private readonly attachmentApi = inject(AttachmentApiService);
  private readonly fb = inject(FormBuilder);

  readonly caseId = input.required<string>();
  readonly sectionRef = viewChild<ElementRef<HTMLElement>>('notesTimelineSection');
  readonly fileInputRef = viewChild<ElementRef<HTMLInputElement>>('attachmentFileInput');

  readonly noteTypes = CASE_NOTE_TYPES;
  readonly notes = signal<CaseNoteDto[]>([]);
  readonly loading = signal(true);
  readonly submitting = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly uploadErrorMessage = signal<string | null>(null);
  readonly selectedFile = signal<File | null>(null);

  readonly noteForm = this.fb.nonNullable.group({
    noteType: ['General' as CaseNoteType, Validators.required],
    bodyText: ['', [Validators.required, Validators.maxLength(4000)]],
    actionRequired: [false],
    actionDueAtLocal: [''],
  });

  async ngOnInit(): Promise<void> {
    await this.loadNotes();
  }

  scrollIntoView(): void {
    this.sectionRef()?.nativeElement.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }

  async loadNotes(): Promise<void> {
    this.loading.set(true);
    this.errorMessage.set(null);

    try {
      const items = await this.caseApi.listCaseNotes(this.caseId());
      this.notes.set(items);
    } catch (error) {
      this.errorMessage.set(this.caseApi.extractErrorMessage(error));
    } finally {
      this.loading.set(false);
    }
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;
    this.uploadErrorMessage.set(null);

    if (!file) {
      this.selectedFile.set(null);
      return;
    }

    if (!ALLOWED_ATTACHMENT_CONTENT_TYPES.includes(file.type as (typeof ALLOWED_ATTACHMENT_CONTENT_TYPES)[number])) {
      this.uploadErrorMessage.set('File type not allowed. Use JPEG, PNG, WebP, or PDF.');
      this.selectedFile.set(null);
      input.value = '';
      return;
    }

    if (file.size > MAX_ATTACHMENT_BYTES) {
      this.uploadErrorMessage.set('File exceeds 10 MiB limit.');
      this.selectedFile.set(null);
      input.value = '';
      return;
    }

    this.selectedFile.set(file);
  }

  async submitNote(): Promise<void> {
    if (this.submitting() || this.noteForm.invalid) {
      this.noteForm.markAllAsTouched();
      return;
    }

    const value = this.noteForm.getRawValue();
    const bodyText = value.bodyText.trim();
    if (!bodyText) {
      this.noteForm.controls.bodyText.setErrors({ required: true });
      return;
    }

    let actionDueAtUtc: string | null = null;
    if (value.actionRequired) {
      if (!value.actionDueAtLocal) {
        this.uploadErrorMessage.set('Action due date is required when action is required.');
        return;
      }

      const due = new Date(value.actionDueAtLocal);
      if (Number.isNaN(due.getTime()) || due.getTime() <= Date.now()) {
        this.uploadErrorMessage.set('Action due date must be in the future.');
        return;
      }

      actionDueAtUtc = due.toISOString();
    }

    this.submitting.set(true);
    this.uploadErrorMessage.set(null);

    const request: CreateCaseNoteRequest = {
      noteType: value.noteType,
      bodyText,
      actionRequired: value.actionRequired || !!actionDueAtUtc,
      actionDueAtUtc,
    };

    try {
      const created = await this.caseApi.createCaseNote(this.caseId(), request);
      const file = this.selectedFile();

      if (file && created.id) {
        await this.uploadAttachment(created.id, file);
        await this.loadNotes();
      } else {
        this.notes.set([...this.notes(), created]);
      }

      this.noteForm.reset({
        noteType: 'General',
        bodyText: '',
        actionRequired: false,
        actionDueAtLocal: '',
      });
      this.selectedFile.set(null);
      const fileInput = this.fileInputRef()?.nativeElement;
      if (fileInput) {
        fileInput.value = '';
      }
    } catch (error) {
      this.uploadErrorMessage.set(this.caseApi.extractErrorMessage(error));
    } finally {
      this.submitting.set(false);
    }
  }

  private async uploadAttachment(noteId: string, file: File): Promise<void> {
    try {
      const presign = await this.attachmentApi.presign({
        resourceType: 'CaseNote',
        resourceId: noteId,
        fileName: attachmentBasename(file.name),
        contentType: file.type,
        fileSizeBytes: file.size,
      });

      if (!presign.uploadUrl || !presign.attachmentId) {
        throw new Error('Presign failed');
      }

      await this.attachmentApi.uploadToPresignedUrl(
        presign.uploadUrl,
        file,
        presign.requiredHeaders ?? {
          'x-ms-blob-type': 'BlockBlob',
          'Content-Type': file.type,
        },
      );

      await this.attachmentApi.confirm({ attachmentId: presign.attachmentId });
    } catch (error) {
      this.uploadErrorMessage.set(this.attachmentApi.extractErrorMessage(error));
    }
  }

  async openAttachment(attachmentId: string | undefined): Promise<void> {
    if (!attachmentId) {
      return;
    }

    try {
      const result = await this.attachmentApi.getDownloadUrl(attachmentId);
      if (result.downloadUrl) {
        window.open(result.downloadUrl, '_blank', 'noopener');
      }
    } catch (error) {
      this.uploadErrorMessage.set(this.attachmentApi.extractDownloadErrorMessage(error));
    }
  }

  authorLabel(note: CaseNoteDto): string {
    if (note.authorEmail) {
      return note.authorEmail;
    }

    const id = note.authorUserId ?? '';
    return id.length > 8 ? `${id.slice(0, 8)}…` : id || 'Unknown';
  }

  formatTimestamp(value: string | undefined): string {
    if (!value) {
      return '';
    }

    return new Date(value).toLocaleString();
  }

  badgeClass(noteType: string | null | undefined): string {
    switch (noteType) {
      case 'Visit':
        return 'badge badge-visit';
      case 'Court':
        return 'badge badge-court';
      case 'Intervention':
        return 'badge badge-intervention';
      default:
        return 'badge badge-general';
    }
  }
}
