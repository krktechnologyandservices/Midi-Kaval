export function formatMatchedOn(value: string | null | undefined): string {
  switch (value) {
    case 'CrimeNumber':
      return 'Matched on Crime number';
    case 'StNumber':
      return 'Matched on ST number';
    case 'Both':
      return 'Matched on Crime and ST number';
    default:
      return 'Possible match';
  }
}
