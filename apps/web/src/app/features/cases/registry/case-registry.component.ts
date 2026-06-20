import {
  Component,
  ElementRef,
  HostListener,
  OnInit,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import {
  CASE_STAGES,
  CaseSearchFiltersDto,
  CaseSearchPresetDto,
  CaseSummaryDto,
  DOMICILE_OPTIONS,
  OFFENCE_CLASSIFICATIONS,
} from '../models/case.models';
import { CaseApiService } from '../services/case-api.service';

@Component({
  selector: 'app-case-registry',
  imports: [
    FormsModule,
    RouterLink,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatCheckboxModule,
    MatTableModule,
    MatPaginatorModule,
  ],
  templateUrl: './case-registry.component.html',
  styleUrl: './case-registry.component.scss',
})
export class CaseRegistryComponent implements OnInit {
  private readonly caseApi = inject(CaseApiService);
  private readonly router = inject(Router);

  readonly searchInput = viewChild<ElementRef<HTMLInputElement>>('searchInput');

  readonly offenceClassifications = OFFENCE_CLASSIFICATIONS;
  readonly domicileOptions = DOMICILE_OPTIONS;
  readonly caseStages = CASE_STAGES;
  readonly displayedColumns = [
    'crimeNumber',
    'stNumber',
    'beneficiaryName',
    'currentStage',
    'typeOfOffence',
    'domicile',
    'visitCount',
  ];

  readonly query = signal('');
  readonly currentStage = signal('');
  readonly typeOfOffence = signal('');
  readonly offenceClassification = signal('');
  readonly domicile = signal('');
  readonly overdue = signal(false);
  readonly items = signal<CaseSummaryDto[]>([]);
  readonly totalCount = signal(0);
  readonly page = signal(0);
  readonly pageSize = signal(25);
  readonly loading = signal(false);
  readonly exporting = signal(false);
  readonly errorMessage = signal('');
  readonly presets = signal<CaseSearchPresetDto[]>([]);
  readonly presetName = signal('');
  readonly selectedPresetId = signal('');

  readonly resultAnnouncement = computed(() => {
    if (this.loading()) {
      return 'Searching cases…';
    }

    const count = this.totalCount();
    if (count === 0) {
      return 'No cases match your filters.';
    }

    return `${count} case${count === 1 ? '' : 's'} found`;
  });

  ngOnInit(): void {
    void this.loadPresets();
    void this.search();
  }

  @HostListener('document:keydown', ['$event'])
  onDocumentKeydown(event: KeyboardEvent): void {
    if (event.key !== '/' || event.ctrlKey || event.metaKey || event.altKey) {
      return;
    }

    const target = event.target;
    if (
      target instanceof HTMLInputElement
      || target instanceof HTMLTextAreaElement
      || target instanceof HTMLSelectElement
      || (target instanceof HTMLElement && target.isContentEditable)
    ) {
      return;
    }

    event.preventDefault();
    this.searchInput()?.nativeElement.focus();
  }

  async search(pageIndex = 0): Promise<void> {
    this.loading.set(true);
    this.errorMessage.set('');

    try {
      const { result, totalCount } = await this.caseApi.searchCases({
        q: this.query(),
        currentStage: this.currentStage() || undefined,
        typeOfOffence: this.typeOfOffence() || undefined,
        offenceClassification: this.offenceClassification() || undefined,
        domicile: this.domicile() || undefined,
        overdue: this.overdue() || undefined,
        page: pageIndex + 1,
        pageSize: this.pageSize(),
      });

      this.items.set(result.items ?? []);
      this.totalCount.set(totalCount);
      this.page.set(pageIndex);
    } catch (error) {
      this.errorMessage.set(this.caseApi.extractErrorMessage(error));
    } finally {
      this.loading.set(false);
    }
  }

  applyFilters(): void {
    void this.search(0);
  }

  exportDisabled(): boolean {
    return this.loading() || this.exporting() || this.totalCount() === 0;
  }

  async exportExcel(): Promise<void> {
    await this.exportCases('xlsx');
  }

  async exportPdf(): Promise<void> {
    await this.exportCases('pdf');
  }

  private async exportCases(format: 'xlsx' | 'pdf'): Promise<void> {
    this.exporting.set(true);
    this.errorMessage.set('');

    try {
      await this.caseApi.exportCases(format, this.buildSearchParams());
    } catch (error) {
      this.errorMessage.set(this.caseApi.extractErrorMessage(error));
    } finally {
      this.exporting.set(false);
    }
  }

  private buildSearchParams() {
    return {
      q: this.query(),
      currentStage: this.currentStage() || undefined,
      typeOfOffence: this.typeOfOffence() || undefined,
      offenceClassification: this.offenceClassification() || undefined,
      domicile: this.domicile() || undefined,
      overdue: this.overdue() || undefined,
    };
  }

  onPageChange(event: PageEvent): void {
    this.pageSize.set(event.pageSize);
    void this.search(event.pageIndex);
  }

  openCase(row: CaseSummaryDto): void {
    if (!row.id) {
      return;
    }

    void this.router.navigate(['/cases', row.id], { state: { summary: row } });
  }

  async loadPresets(): Promise<void> {
    try {
      const presets = await this.caseApi.listSearchPresets();
      this.presets.set(presets);
    } catch (error) {
      this.errorMessage.set(this.caseApi.extractErrorMessage(error));
    }
  }

  applyPreset(presetId: string): void {
    const preset = this.presets().find((p) => p.id === presetId);
    if (!preset?.filters) {
      return;
    }

    this.selectedPresetId.set(presetId);
    this.applyFiltersFromDto(preset.filters);
    void this.search(0);
  }

  async savePreset(): Promise<void> {
    const name = this.presetName().trim();
    if (!name) {
      this.errorMessage.set('Preset name is required.');
      return;
    }

    try {
      await this.caseApi.createSearchPreset({
        name,
        filters: this.buildFiltersDto(),
      });
      this.presetName.set('');
      await this.loadPresets();
    } catch (error) {
      this.errorMessage.set(this.caseApi.extractErrorMessage(error));
    }
  }

  private applyFiltersFromDto(filters: CaseSearchFiltersDto): void {
    this.query.set(filters.q ?? '');
    this.currentStage.set(filters.currentStage ?? '');
    this.typeOfOffence.set(filters.typeOfOffence ?? '');
    this.offenceClassification.set(filters.offenceClassification ?? '');
    this.domicile.set(filters.domicile ?? '');
    this.overdue.set(filters.overdue === true);
  }

  private buildFiltersDto(): CaseSearchFiltersDto {
    return {
      q: this.query().trim() || undefined,
      currentStage: this.currentStage() || undefined,
      typeOfOffence: this.typeOfOffence().trim() || undefined,
      offenceClassification: this.offenceClassification() || undefined,
      domicile: this.domicile() || undefined,
      overdue: this.overdue() || undefined,
    };
  }
}
