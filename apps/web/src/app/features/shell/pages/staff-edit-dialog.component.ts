import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { STAFF_ROLES, StaffDto } from '../staff.models';

export interface StaffEditData {
  title: string;
  staff?: StaffDto;
}

export interface StaffEditResult {
  firstName: string;
  lastName: string;
  phoneNumber: string;
  role?: string;
}

@Component({
  selector: 'app-staff-edit-dialog',
  imports: [
    MatDialogModule, MatButtonModule, MatFormFieldModule,
    MatInputModule, MatSelectModule, FormsModule,
  ],
  template: `
    <h2 mat-dialog-title>{{ data.title }}</h2>
    <mat-dialog-content>
      <mat-form-field appearance="outline" class="full-width">
        <mat-label>Email</mat-label>
        <input matInput [(ngModel)]="email" [disabled]="!!data.staff" />
      </mat-form-field>
      <div class="row">
        <mat-form-field appearance="outline" class="half-width">
          <mat-label>First name</mat-label>
          <input matInput [(ngModel)]="firstName" />
        </mat-form-field>
        <mat-form-field appearance="outline" class="half-width">
          <mat-label>Last name</mat-label>
          <input matInput [(ngModel)]="lastName" />
        </mat-form-field>
      </div>
      <mat-form-field appearance="outline" class="full-width">
        <mat-label>Phone number</mat-label>
        <input matInput [(ngModel)]="phoneNumber" placeholder="Optional" />
      </mat-form-field>
      <mat-form-field appearance="outline" class="full-width">
        <mat-label>Role</mat-label>
        <mat-select [(ngModel)]="role">
          @for (r of roleOptions; track r.value) {
            <mat-option [value]="r.value">{{ r.label }}</mat-option>
          }
        </mat-select>
      </mat-form-field>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button (click)="cancel()">Cancel</button>
      <button
        mat-flat-button
        color="primary"
        [disabled]="!canSave()"
        (click)="save()"
      >
        Save
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .full-width { width: 100%; }
    .half-width { width: calc(50% - 6px); }
    .row { display: flex; gap: 12px; }
    mat-dialog-content { min-width: 400px; padding-top: 8px; }
  `,
})
export class StaffEditDialogComponent {
  private readonly dialogRef = inject(MatDialogRef<StaffEditDialogComponent, StaffEditResult>);
  readonly data = inject<StaffEditData>(MAT_DIALOG_DATA);

  readonly roleOptions = STAFF_ROLES;

  email: string;
  firstName: string;
  lastName: string;
  phoneNumber: string;
  role: string;

  constructor() {
    if (this.data.staff) {
      this.email = this.data.staff.email;
      this.firstName = this.data.staff.firstName;
      this.lastName = this.data.staff.lastName;
      this.phoneNumber = this.data.staff.phoneNumber ?? '';
      this.role = this.data.staff.role;
    } else {
      this.email = '';
      this.firstName = '';
      this.lastName = '';
      this.phoneNumber = '';
      this.role = '';
    }
  }

  canSave(): boolean {
    return (
      this.firstName.trim().length > 0 &&
      this.lastName.trim().length > 0 &&
      (!!this.data.staff || this.email.trim().length > 0) &&
      this.role.length > 0
    );
  }

  save(): void {
    if (!this.canSave()) {
      this.dialogRef.close();
      return;
    }
    this.dialogRef.close({
      firstName: this.firstName.trim(),
      lastName: this.lastName.trim(),
      phoneNumber: this.phoneNumber.trim(),
      role: this.role,
    });
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
