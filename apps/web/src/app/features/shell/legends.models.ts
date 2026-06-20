export interface LegendDto {
  id: string;
  name: string;
  isActive: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export const LEGEND_TYPE_OPTIONS = [
  { value: 'offence-types', label: 'Offence Types' },
  { value: 'classifications', label: 'Classifications' },
  { value: 'intervention-categories', label: 'Intervention Categories' },
  { value: 'education-levels', label: 'Education Levels' },
  { value: 'occupations', label: 'Occupations' },
  { value: 'visit-outcomes', label: 'Visit Outcomes' },
  { value: 'court-outcomes', label: 'Court Outcomes' },
  { value: 'areas', label: 'Areas' },
  { value: 'designations', label: 'Designations' },
  { value: 'police-stations', label: 'Police Stations' },
] as const;
