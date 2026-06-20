import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { CaseApiService, CaseApiError } from '../services/case-api.service';
import { CaseRegistryComponent } from './case-registry.component';
import { CaseSummaryDto } from '../models/case.models';

describe('CaseRegistryComponent', () => {
  let fixture: ComponentFixture<CaseRegistryComponent>;
  let caseApi: jasmine.SpyObj<CaseApiService>;
  let router: Router;

  const sampleRow: CaseSummaryDto = {
    id: '11111111-1111-4111-8111-111111111111',
    crimeNumber: 'CR-1',
    stNumber: 'ST-1',
    beneficiaryName: 'Test User',
    currentStage: 'ProcessInitiation',
    typeOfOffence: 'Theft',
    offenceClassification: 'Petty',
    domicile: 'Urban',
    visitCount: 0,
    createdByUserId: '22222222-2222-4222-8222-222222222222',
    updatedAtUtc: '2026-06-15T00:00:00Z',
  };

  beforeEach(async () => {
    caseApi = jasmine.createSpyObj<CaseApiService>('CaseApiService', [
      'searchCases',
      'listSearchPresets',
      'createSearchPreset',
      'exportCases',
      'extractErrorMessage',
    ]);

    caseApi.searchCases.and.returnValue(
      Promise.resolve({
        result: { items: [sampleRow], page: 1, pageSize: 25 },
        totalCount: 1,
      }),
    );
    caseApi.listSearchPresets.and.returnValue(
      Promise.resolve([
        {
          id: '33333333-3333-4333-8333-333333333333',
          name: 'Urban only',
          filters: { domicile: 'Urban' },
          createdAtUtc: '2026-06-15T00:00:00Z',
        },
      ]),
    );
    caseApi.createSearchPreset.and.returnValue(
      Promise.resolve({
        id: '44444444-4444-4444-8444-444444444444',
        name: 'Saved',
        filters: {},
        createdAtUtc: '2026-06-15T00:00:00Z',
      }),
    );
    caseApi.extractErrorMessage.and.returnValue('Search failed');
    caseApi.exportCases.and.returnValue(Promise.resolve());

    await TestBed.configureTestingModule({
      imports: [CaseRegistryComponent],
      providers: [provideRouter([]), { provide: CaseApiService, useValue: caseApi }],
    }).compileComponents();

    fixture = TestBed.createComponent(CaseRegistryComponent);
    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.returnValue(Promise.resolve(true));
  });

  async function settle(): Promise<void> {
    fixture.detectChanges();
    await fixture.whenStable();
    await Promise.resolve();
    fixture.detectChanges();
  }

  it('renders search results on load', async () => {
    await settle();

    expect(caseApi.searchCases).toHaveBeenCalled();
    expect(fixture.nativeElement.textContent).toContain('CR-1');
    expect(fixture.nativeElement.textContent).toContain('1 case found');
  });

  it('applies filters when Apply filters is clicked', async () => {
    await settle();
    caseApi.searchCases.calls.reset();

    fixture.componentInstance.domicile.set('Rural');
    fixture.detectChanges();
    const applyButton = Array.from(
      fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>,
    ).find((el) => el.textContent?.includes('Apply filters'));
    expect(applyButton).toBeTruthy();
    applyButton!.click();
    await settle();

    expect(caseApi.searchCases).toHaveBeenCalledWith(
      jasmine.objectContaining({ domicile: 'Rural', page: 1 }),
    );
  });

  it('loads a preset into the form and searches', async () => {
    await settle();
    caseApi.searchCases.calls.reset();

    fixture.componentInstance.applyPreset('33333333-3333-4333-8333-333333333333');
    await settle();

    expect(fixture.componentInstance.domicile()).toBe('Urban');
    expect(caseApi.searchCases).toHaveBeenCalled();
  });

  it('saves the current filters as a preset', async () => {
    await settle();

    fixture.componentInstance.presetName.set('My preset');
    await fixture.componentInstance.savePreset();

    expect(caseApi.createSearchPreset).toHaveBeenCalled();
    expect(caseApi.listSearchPresets).toHaveBeenCalledTimes(2);
  });

  it('focuses search input when / is pressed outside editable fields', () => {
    fixture.detectChanges();
    const input = fixture.componentInstance.searchInput()?.nativeElement;
    expect(input).toBeTruthy();

    document.dispatchEvent(new KeyboardEvent('keydown', { key: '/' }));
    expect(document.activeElement).toBe(input!);
  });

  it('navigates to case detail on row click', async () => {
    fixture.detectChanges();
    await fixture.whenStable();

    fixture.componentInstance.openCase(sampleRow);

    expect(router.navigate).toHaveBeenCalledWith(
      ['/cases', sampleRow.id],
      jasmine.objectContaining({ state: { summary: sampleRow } }),
    );
  });

  it('shows zero-results copy when no matches', async () => {
    caseApi.searchCases.and.returnValue(
      Promise.resolve({ result: { items: [], page: 1, pageSize: 25 }, totalCount: 0 }),
    );

    await settle();

    expect(fixture.nativeElement.textContent).toContain('No cases match your filters.');
  });

  it('keeps form editable and shows error when search fails', async () => {
    await settle();

    caseApi.searchCases.and.callFake(async () => {
      throw new Error('network');
    });

    await fixture.componentInstance.applyFilters();
    fixture.detectChanges();

    expect(fixture.componentInstance.errorMessage()).toBe('Search failed');
    expect(fixture.componentInstance.items()).toEqual([sampleRow]);
    expect(fixture.nativeElement.querySelector('input[matinput]')).toBeTruthy();
  });

  it('disables export buttons when totalCount is 0', async () => {
    caseApi.searchCases.and.returnValue(
      Promise.resolve({ result: { items: [], page: 1, pageSize: 25 }, totalCount: 0 }),
    );

    await settle();

    const exportButtons = Array.from(
      fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>,
    ).filter((el) => el.textContent?.includes('Export'));
    expect(exportButtons.length).toBe(2);
    exportButtons.forEach((button) => expect(button.disabled).toBeTrue());
  });

  it('calls exportCases with current filters when Export Excel is clicked', async () => {
    await settle();
    caseApi.exportCases.calls.reset();

    fixture.componentInstance.domicile.set('Urban');
    fixture.detectChanges();

    const exportButton = Array.from(
      fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>,
    ).find((el) => el.textContent?.includes('Export Excel'));
    expect(exportButton).toBeTruthy();
    exportButton!.click();
    await settle();

    expect(caseApi.exportCases).toHaveBeenCalledWith(
      'xlsx',
      jasmine.objectContaining({ domicile: 'Urban' }),
    );
  });

  it('disables export buttons while exporting', async () => {
    await settle();

    let resolveExport: (() => void) | undefined;
    caseApi.exportCases.and.returnValue(
      new Promise<void>((resolve) => {
        resolveExport = resolve;
      }),
    );

    const exportPromise = fixture.componentInstance.exportExcel();
    fixture.detectChanges();

    const exportButtons = Array.from(
      fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>,
    ).filter((el) => el.textContent?.includes('Export'));
    exportButtons.forEach((button) => expect(button.disabled).toBeTrue());

    resolveExport?.();
    await exportPromise;
    await settle();
  });

  it('shows 422 over-cap detail in errorMessage and keeps filters editable', async () => {
    await settle();

    const overCapDetail = 'Export limited to 5000 cases; refine filters and try again.';
    caseApi.exportCases.and.returnValue(
      Promise.reject(new CaseApiError('http', 422, { error: { detail: overCapDetail } })),
    );
    caseApi.extractErrorMessage.and.callFake((error: unknown) => {
      if (error instanceof CaseApiError && error.status === 422) {
        return overCapDetail;
      }
      return 'Search failed';
    });

    await fixture.componentInstance.exportExcel();
    fixture.detectChanges();

    expect(fixture.componentInstance.errorMessage()).toBe(overCapDetail);
    expect(fixture.componentInstance.items()).toEqual([sampleRow]);
    expect(fixture.nativeElement.querySelector('input[matinput]')).toBeTruthy();
  });
});
