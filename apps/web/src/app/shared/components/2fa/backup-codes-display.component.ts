import { Component, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'app-backup-codes-display',
  standalone: true,
  imports: [MatButtonModule, MatIconModule],
  template: `
    <div class="backup-codes-container">
      <p class="backup-codes-intro">
        Save these backup codes in a safe place. You can use them to sign in if you
        lose access to your authenticator app.
      </p>
      <div class="codes-grid">
        @for (code of codes(); track $index) {
          <code class="backup-code-chip">{{ code }}</code>
        }
      </div>
      <div class="backup-codes-actions">
        <button mat-stroked-button color="primary" (click)="downloadTxt()">
          <mat-icon>download</mat-icon> Download as .txt
        </button>
        <button mat-raised-button color="primary" (click)="saved.emit()">
          I've saved my backup codes
        </button>
      </div>
    </div>
  `,
  styles: [
    `
    .backup-codes-container {
      display: flex;
      flex-direction: column;
      gap: 16px;
    }
    .backup-codes-intro {
      font-size: 14px;
      line-height: 1.5;
      color: var(--text-secondary, #64748B);
      margin: 0;
    }
    .codes-grid {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 8px;
    }
    .backup-code-chip {
      display: flex;
      align-items: center;
      justify-content: center;
      padding: 10px 12px;
      background: var(--surface-secondary, #F5F6FA);
      border-radius: 6px;
      font-family: 'Courier New', Courier, monospace;
      font-size: 13px;
      letter-spacing: 1px;
      user-select: all;
    }
    .backup-codes-actions {
      display: flex;
      gap: 12px;
      justify-content: center;
      margin-top: 8px;
    }
    button { min-height: 48px; }
  `,
  ],
})
export class BackupCodesDisplayComponent {
  readonly codes = input<string[]>([]);
  readonly email = input<string>('');
  readonly saved = output<void>();

  downloadTxt(): void {
    const header = `Kaval Online Backup Codes — ${this.email()}\n${'='.repeat(40)}\n\n`;
    const content = this.codes().join('\n');
    const blob = new Blob([header + content], { type: 'text/plain' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = 'kaval-backup-codes.txt';
    a.click();
    URL.revokeObjectURL(url);
  }
}
