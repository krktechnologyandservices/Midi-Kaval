import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatTableModule } from '@angular/material/table';
import { MatDialog } from '@angular/material/dialog';
import { CommonModule } from '@angular/common';
import { firstValueFrom } from 'rxjs';
import { LegendsApiService } from '../services/legends-api.service';
import { LegendDto, LEGEND_TYPE_OPTIONS } from '../legends.models';
import { LegendEditDialogComponent, LegendEditData } from './legend-edit-dialog.component';
import { LegendConfirmDialogComponent, ConfirmDialogData } from './legend-confirm-dialog.component';

@Component({
  selector: 'app-legends-page',
  imports: [
    CommonModule, MatButtonModule, MatCardModule, MatIconModule,
    MatSelectModule, MatFormFieldModule, MatTableModule,
  ],
  templateUrl: './legends-page.component.html',
  styleUrl: './legends-page.component.scss',
})
export class LegendsPageComponent implements OnInit {
  private readonly api = inject(LegendsApiService);
  private readonly dialog = inject(MatDialog);

  readonly typeOptions = LEGEND_TYPE_OPTIONS;
  readonly selectedType = signal<string>(this.typeOptions[0].value);
  readonly items = signal<LegendDto[]>([]);
  readonly loading = signal(false);
  readonly errorMessage = signal<string | null>(null);

  readonly displayedColumns = ['name', 'status', 'actions'];

  readonly selectedTypeLabel = computed(
    () => this.typeOptions.find((o) => o.value === this.selectedType())?.label ?? '',
  );

  readonly isEmpty = computed(() => this.items().length === 0 && !this.loading());

  async ngOnInit(): Promise<void> {
    await this.loadItems();
  }

  async onTypeChange(): Promise<void> {
    await this.loadItems();
  }

  async loadItems(): Promise<void> {
    this.loading.set(true);
    this.errorMessage.set(null);
    try {
      const items = await this.api.list(this.selectedType());
      this.items.set(items);
    } catch (error) {
      this.errorMessage.set(this.api.extractErrorMessage(error));
    } finally {
      this.loading.set(false);
    }
  }

  async add(): Promise<void> {
    const result = await firstValueFrom(
      this.dialog.open(LegendEditDialogComponent, {
        data: { title: `Add ${this.selectedTypeLabel()}`, currentName: '' } satisfies LegendEditData,
        width: '420px',
      }).afterClosed(),
    );

    if (!result?.name) {
      return;
    }

    this.errorMessage.set(null);
    try {
      await this.api.create(this.selectedType(), result.name);
      await this.loadItems();
    } catch (error) {
      this.errorMessage.set(this.api.extractErrorMessage(error));
    }
  }

  async edit(item: LegendDto): Promise<void> {
    const result = await firstValueFrom(
      this.dialog.open(LegendEditDialogComponent, {
        data: { title: `Edit ${this.selectedTypeLabel()}`, currentName: item.name } satisfies LegendEditData,
        width: '420px',
      }).afterClosed(),
    );

    if (!result?.name || result.name.toLowerCase() === item.name.toLowerCase()) {
      return;
    }

    this.errorMessage.set(null);
    try {
      await this.api.update(this.selectedType(), item.id, result.name);
      await this.loadItems();
    } catch (error) {
      this.errorMessage.set(this.api.extractErrorMessage(error));
    }
  }

  async deactivate(item: LegendDto): Promise<void> {
    const confirmed = await firstValueFrom(
      this.dialog.open(LegendConfirmDialogComponent, {
        data: {
          title: 'Deactivate',
          message: `Deactivate "${item.name}"? Inactive entries can be re-activated later.`,
          confirmLabel: 'Deactivate',
        } satisfies ConfirmDialogData,
        width: '420px',
      }).afterClosed(),
    );

    if (!confirmed) {
      return;
    }

    this.errorMessage.set(null);
    try {
      await this.api.deactivate(this.selectedType(), item.id);
      await this.loadItems();
    } catch (error) {
      this.errorMessage.set(this.api.extractErrorMessage(error));
    }
  }

  async reactivate(item: LegendDto): Promise<void> {
    const confirmed = await firstValueFrom(
      this.dialog.open(LegendConfirmDialogComponent, {
        data: {
          title: 'Reactivate',
          message: `Reactivate "${item.name}"? It will become active and available for selection again.`,
          confirmLabel: 'Reactivate',
        } satisfies ConfirmDialogData,
        width: '420px',
      }).afterClosed(),
    );

    if (!confirmed) {
      return;
    }

    this.errorMessage.set(null);
    try {
      await this.api.reactivate(this.selectedType(), item.id);
      await this.loadItems();
    } catch (error) {
      this.errorMessage.set(this.api.extractErrorMessage(error));
    }
  }
}
