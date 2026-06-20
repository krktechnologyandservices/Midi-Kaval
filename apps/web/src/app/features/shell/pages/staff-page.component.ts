import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { MatDialog } from '@angular/material/dialog';
import { CommonModule } from '@angular/common';
import { firstValueFrom } from 'rxjs';
import { StaffApiService } from '../services/staff-api.service';
import { StaffDto, STAFF_ROLES } from '../staff.models';
import { StaffEditDialogComponent, StaffEditData } from './staff-edit-dialog.component';
import { StaffConfirmDialogComponent, StaffConfirmDialogData } from './staff-confirm-dialog.component';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-staff-page',
  imports: [
    CommonModule, MatButtonModule, MatCardModule, MatIconModule,
    MatTableModule, RouterLink,
  ],
  templateUrl: './staff-page.component.html',
  styleUrl: './staff-page.component.scss',
})
export class StaffPageComponent implements OnInit {
  private readonly api = inject(StaffApiService);
  private readonly dialog = inject(MatDialog);

  readonly items = signal<StaffDto[]>([]);
  readonly loading = signal(false);
  readonly errorMessage = signal<string | null>(null);

  readonly displayedColumns = ['name', 'email', 'role', 'status', 'actions'];
  readonly roleLabels = new Map(STAFF_ROLES.map((r) => [r.value, r.label]));

  readonly isEmpty = computed(() => this.items().length === 0 && !this.loading());

  async ngOnInit(): Promise<void> {
    await this.loadItems();
  }

  async loadItems(): Promise<void> {
    this.loading.set(true);
    this.errorMessage.set(null);
    try {
      const items = await this.api.list();
      this.items.set(items);
    } catch (error) {
      this.errorMessage.set(this.api.extractErrorMessage(error));
    } finally {
      this.loading.set(false);
    }
  }

  async add(): Promise<void> {
    const result = await firstValueFrom(
      this.dialog.open(StaffEditDialogComponent, {
        data: { title: 'Add staff member' } satisfies StaffEditData,
        width: '440px',
      }).afterClosed(),
    );

    if (!result) {
      return;
    }

    this.errorMessage.set(null);
    try {
      await this.api.create({
        email: result.email ?? '',
        firstName: result.firstName,
        lastName: result.lastName,
        phoneNumber: result.phoneNumber || undefined,
        role: result.role ?? 'CaseWorker',
      });
      await this.loadItems();
    } catch (error) {
      this.errorMessage.set(this.api.extractErrorMessage(error));
    }
  }

  async edit(item: StaffDto): Promise<void> {
    const result = await firstValueFrom(
      this.dialog.open(StaffEditDialogComponent, {
        data: { title: 'Edit staff member', staff: item } satisfies StaffEditData,
        width: '440px',
      }).afterClosed(),
    );

    if (!result) {
      return;
    }

    this.errorMessage.set(null);
    try {
      await this.api.update(item.id, {
        firstName: result.firstName,
        lastName: result.lastName,
        phoneNumber: result.phoneNumber || undefined,
        role: result.role !== item.role ? result.role : undefined,
      });
      await this.loadItems();
    } catch (error) {
      this.errorMessage.set(this.api.extractErrorMessage(error));
    }
  }

  async deactivate(item: StaffDto): Promise<void> {
    const confirmed = await firstValueFrom(
      this.dialog.open(StaffConfirmDialogComponent, {
        data: {
          title: 'Deactivate staff member',
          message: `Deactivate "${item.firstName} ${item.lastName}" (${item.email})? They will no longer be able to log in. You can reactivate them later.`,
          confirmLabel: 'Deactivate',
        } satisfies StaffConfirmDialogData,
        width: '420px',
      }).afterClosed(),
    );

    if (!confirmed) {
      return;
    }

    this.errorMessage.set(null);
    try {
      await this.api.deactivate(item.id);
      await this.loadItems();
    } catch (error) {
      this.errorMessage.set(this.api.extractErrorMessage(error));
    }
  }

  async reactivate(item: StaffDto): Promise<void> {
    const confirmed = await firstValueFrom(
      this.dialog.open(StaffConfirmDialogComponent, {
        data: {
          title: 'Reactivate staff member',
          message: `Reactivate "${item.firstName} ${item.lastName}" (${item.email})? They will regain login access.`,
          confirmLabel: 'Reactivate',
        } satisfies StaffConfirmDialogData,
        width: '420px',
      }).afterClosed(),
    );

    if (!confirmed) {
      return;
    }

    this.errorMessage.set(null);
    try {
      await this.api.reactivate(item.id);
      await this.loadItems();
    } catch (error) {
      this.errorMessage.set(this.api.extractErrorMessage(error));
    }
  }

  async forceReset(item: StaffDto): Promise<void> {
    const confirmed = await firstValueFrom(
      this.dialog.open(StaffConfirmDialogComponent, {
        data: {
          title: 'Force session reset',
          message: `Force "${item.firstName} ${item.lastName}" (${item.email}) to log in again? All their active sessions will be invalidated.`,
          confirmLabel: 'Force reset',
        } satisfies StaffConfirmDialogData,
        width: '420px',
      }).afterClosed(),
    );

    if (!confirmed) {
      return;
    }

    this.errorMessage.set(null);
    try {
      await this.api.forceReset(item.id);
      // Refresh to see updated token_version (not displayed, but keep data fresh)
      await this.loadItems();
    } catch (error) {
      this.errorMessage.set(this.api.extractErrorMessage(error));
    }
  }
}
