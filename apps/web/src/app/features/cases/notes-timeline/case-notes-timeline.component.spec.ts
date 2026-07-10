import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { CaseNotesTimelineComponent } from './case-notes-timeline.component';
import { CaseApiService } from '../services/case-api.service';
import { AttachmentApiService } from '../services/attachment-api.service';
import { CaseNoteDto } from '../models/case.models';

describe('CaseNotesTimelineComponent', () => {
  let fixture: ComponentFixture<CaseNotesTimelineComponent>;
  let caseApi: jasmine.SpyObj<CaseApiService>;
  let attachmentApi: jasmine.SpyObj<AttachmentApiService>;

  const caseId = '11111111-1111-4111-8111-111111111111';

  const notes: CaseNoteDto[] = [
    {
      id: 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
      caseId,
      noteType: 'Visit',
      bodyText: 'Older note',
      authorEmail: 'worker@test',
      createdAtUtc: '2026-06-10T10:00:00Z',
      attachments: [],
    },
    {
      id: 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb',
      caseId,
      noteType: 'Court',
      bodyText: 'Newer note',
      authorEmail: 'coord@test',
      createdAtUtc: '2026-06-11T10:00:00Z',
      attachments: [{ id: 'cccccccc-cccc-4ccc-8ccc-cccccccccccc', originalFileName: 'brief.pdf' }],
    },
  ];

  beforeEach(async () => {
    caseApi = jasmine.createSpyObj('CaseApiService', [
      'listCaseNotes',
      'createCaseNote',
      'extractErrorMessage',
    ]);
    attachmentApi = jasmine.createSpyObj('AttachmentApiService', [
      'upload',
      'download',
      'extractErrorMessage',
      'extractDownloadErrorMessage',
    ]);

    caseApi.listCaseNotes.and.returnValue(Promise.resolve(notes));
    caseApi.createCaseNote.and.returnValue(
      Promise.resolve({
        id: 'dddddddd-dddd-4ddd-8ddd-dddddddddddd',
        caseId,
        noteType: 'General',
        bodyText: 'Created',
        createdAtUtc: '2026-06-12T10:00:00Z',
        attachments: [],
      }),
    );
    caseApi.extractErrorMessage.and.returnValue('Save failed');
    attachmentApi.download.and.returnValue(
      Promise.resolve(new Blob(['pdf'], { type: 'application/pdf' })),
    );

    await TestBed.configureTestingModule({
      imports: [CaseNotesTimelineComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: CaseApiService, useValue: caseApi },
        { provide: AttachmentApiService, useValue: attachmentApi },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(CaseNotesTimelineComponent);
    fixture.componentRef.setInput('caseId', caseId);
  });

  async function settle(): Promise<void> {
    fixture.detectChanges();
    await fixture.whenStable();
    await Promise.resolve();
    fixture.detectChanges();
  }

  it('renders notes in API order with type badges', async () => {
    await settle();

    const text = fixture.nativeElement.textContent as string;
    const visitIndex = text.indexOf('Visit');
    const courtIndex = text.indexOf('Court');
    expect(visitIndex).toBeGreaterThan(-1);
    expect(courtIndex).toBeGreaterThan(visitIndex);
    expect(text).toContain('Older note');
    expect(text).toContain('brief.pdf');
  });

  it('creates a note via CaseApiService', async () => {
    await settle();

    fixture.componentInstance.noteForm.setValue({
      noteType: 'General',
      bodyText: 'New entry',
      actionRequired: false,
      actionDueAtLocal: '',
    });

    await fixture.componentInstance.submitNote();
    await settle();

    expect(caseApi.createCaseNote).toHaveBeenCalledWith(caseId, {
      noteType: 'General',
      bodyText: 'New entry',
      actionRequired: false,
      actionDueAtUtc: null,
    });
  });

  it('downloads attachment when attachment chip clicked', async () => {
    await settle();

    const buttons = fixture.nativeElement.querySelectorAll('.attachment-chip');
    (buttons[0] as HTMLButtonElement).click();
    await settle();

    expect(attachmentApi.download).toHaveBeenCalledWith('cccccccc-cccc-4ccc-8ccc-cccccccccccc');
  });

  it('uploads attachment after note create', async () => {
    await settle();

    const attachmentId = 'eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee';
    attachmentApi.upload.and.returnValue(
      Promise.resolve({
        id: attachmentId,
        resourceType: 'CaseNote',
        resourceId: 'dddddddd-dddd-4ddd-8ddd-dddddddddddd',
        originalFileName: 'brief.pdf',
        contentType: 'application/pdf',
        fileSizeBytes: 3,
        status: 'Confirmed',
        uploadedByUserId: 'worker-1',
        createdAtUtc: '2026-06-12T10:00:00Z',
      }),
    );

    fixture.componentInstance.noteForm.setValue({
      noteType: 'General',
      bodyText: 'Note with file',
      actionRequired: false,
      actionDueAtLocal: '',
    });
    fixture.componentInstance.selectedFile.set(
      new File(['pdf'], 'brief.pdf', { type: 'application/pdf' }),
    );

    await fixture.componentInstance.submitNote();
    await settle();

    expect(attachmentApi.upload).toHaveBeenCalledWith({
      resourceType: 'CaseNote',
      resourceId: 'dddddddd-dddd-4ddd-8ddd-dddddddddddd',
      file: jasmine.any(File),
    });
    expect(caseApi.listCaseNotes).toHaveBeenCalledTimes(2);
  });
});
