import { Component, inject, OnInit, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import {
  MAT_DIALOG_DATA,
  MatDialogModule,
  MatDialogRef,
} from '@angular/material/dialog';
import { CaseDuplicateMatchDto } from '../models/case.models';
import { formatMatchedOn } from '../utils/matched-on-label';

export interface DuplicateMatchSheetData {
  matches: CaseDuplicateMatchDto[];
  canMerge: boolean;
  triggerFieldId: string;
}

export type DuplicateMatchSheetResult =
  | { action: 'open'; caseId: string; match: CaseDuplicateMatchDto }
  | { action: 'merge'; caseId: string; match: CaseDuplicateMatchDto }
  | { action: 'cancel' };

@Component({
  selector: 'app-duplicate-match-sheet',
  imports: [MatDialogModule, MatButtonModule],
  templateUrl: './duplicate-match-sheet.component.html',
  styleUrl: './duplicate-match-sheet.component.scss',
})
export class DuplicateMatchSheetComponent implements OnInit {
  private readonly dialogRef = inject(MatDialogRef<DuplicateMatchSheetComponent, DuplicateMatchSheetResult>);
  readonly data = inject<DuplicateMatchSheetData>(MAT_DIALOG_DATA);

  readonly announcement = signal('');
  readonly confirmingCaseId = signal<string | null>(null);

  readonly formatMatchedOn = formatMatchedOn;

  ngOnInit(): void {
    const count = this.data.matches.length;
    this.announcement.set(
      count === 1 ? '1 possible match found' : `${count} possible matches found`,
    );
  }

  openExisting(match: CaseDuplicateMatchDto): void {
    if (!match.caseId) {
      return;
    }

    this.dialogRef.close({ action: 'open', caseId: match.caseId, match });
  }

  startMergeConfirm(match: CaseDuplicateMatchDto): void {
    if (!match.caseId) {
      return;
    }

    this.confirmingCaseId.set(match.caseId);
  }

  cancelMergeConfirm(): void {
    this.confirmingCaseId.set(null);
  }

  confirmMerge(match: CaseDuplicateMatchDto): void {
    if (!match.caseId) {
      return;
    }

    this.dialogRef.close({ action: 'merge', caseId: match.caseId, match });
  }

  cancel(): void {
    this.dialogRef.close({ action: 'cancel' });
  }

  isConfirming(match: CaseDuplicateMatchDto): boolean {
    return this.confirmingCaseId() === match.caseId;
  }
}
