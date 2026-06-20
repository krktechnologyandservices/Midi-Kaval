export function beneficiaryInitials(fullName?: string | null): string {
  if (!fullName?.trim()) {
    return '—';
  }

  const tokens = fullName.trim().split(/\s+/).filter(Boolean);
  if (tokens.length === 0) {
    return '—';
  }

  if (tokens.length === 1) {
    return `${tokens[0]![0]!.toUpperCase()}.`;
  }

  return `${tokens[0]![0]!.toUpperCase()}. ${tokens[1]![0]!.toUpperCase()}.`;
}

export function isPocsoCase(sensitivityLevel?: string | null): boolean {
  return sensitivityLevel === 'POCSO';
}
