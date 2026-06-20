import React from 'react';
import {Pressable, StyleSheet, Text, View} from 'react-native';
import {beneficiaryInitials, isPocsoCase} from '../utils/beneficiaryInitials';

export type DiscreetCaseHeader = {
  crimeNumber?: string | null;
  stNumber?: string | null;
  domicile?: string | null;
  beneficiaryName?: string | null;
  sensitivityLevel?: string | null;
  beneficiaryContact?: string | null;
  beneficiaryAge?: number | null;
};

type Props = {
  caseInfo: DiscreetCaseHeader;
  expanded: boolean;
  onExpandPress: () => void;
  expandLoading?: boolean;
};

export function DiscreetHeader({
  caseInfo,
  expanded,
  onExpandPress,
  expandLoading = false,
}: Props): React.JSX.Element {
  const pocso = isPocsoCase(caseInfo.sensitivityLevel);
  const initials = beneficiaryInitials(caseInfo.beneficiaryName);

  if (!pocso) {
    const crimeLine = `${caseInfo.crimeNumber ?? '—'} · ${caseInfo.stNumber ?? '—'}`;
    return (
      <View accessibilityLabel="Case header">
        <Text style={styles.crime}>{crimeLine}</Text>
        <Text style={styles.meta}>{caseInfo.domicile ?? '—'}</Text>
      </View>
    );
  }

  const headline = expanded
    ? `${caseInfo.beneficiaryName ?? initials} · ${caseInfo.crimeNumber ?? '—'}`
    : `${initials} · ${caseInfo.crimeNumber ?? '—'}`;

  return (
    <View
      accessibilityLabel={expanded ? 'Full detail mode' : 'Limited detail mode'}>
      <Text style={styles.crime}>{headline}</Text>
      {expanded ? (
        <>
          <Text style={styles.meta}>ST: {caseInfo.stNumber ?? '—'}</Text>
          <Text style={styles.meta}>Domicile: {caseInfo.domicile ?? '—'}</Text>
          <Text style={styles.meta}>
            Beneficiary: {caseInfo.beneficiaryName ?? '—'}
          </Text>
          {caseInfo.beneficiaryContact ? (
            <Text style={styles.meta}>Contact: {caseInfo.beneficiaryContact}</Text>
          ) : null}
          {caseInfo.beneficiaryAge != null ? (
            <Text style={styles.meta}>Age: {caseInfo.beneficiaryAge}</Text>
          ) : null}
        </>
      ) : (
        <Text style={styles.discreetHint}>Limited detail mode</Text>
      )}
      {!expanded ? (
        <Pressable
          onPress={onExpandPress}
          disabled={expandLoading}
          accessibilityRole="button"
          accessibilityLabel="Show full detail">
          <Text style={styles.expandLink}>
            {expandLoading ? 'Verifying…' : 'Show full detail'}
          </Text>
        </Pressable>
      ) : null}
    </View>
  );
}

const styles = StyleSheet.create({
  crime: {
    fontWeight: '600',
    fontSize: 15,
    color: '#101828',
    paddingRight: 88,
    fontVariant: ['tabular-nums'],
  },
  meta: {
    fontSize: 13,
    color: '#475467',
    marginTop: 4,
  },
  discreetHint: {
    fontSize: 12,
    color: '#475467',
    marginTop: 4,
  },
  expandLink: {
    marginTop: 8,
    fontSize: 14,
    fontWeight: '600',
    color: '#175CD3',
  },
});
