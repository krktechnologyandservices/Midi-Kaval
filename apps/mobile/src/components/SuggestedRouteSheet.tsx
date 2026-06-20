import React, {useMemo, useState} from 'react';

import {

  Modal,

  Pressable,

  ScrollView,

  StyleSheet,

  Text,

  View,

} from 'react-native';

import {VisitGroupingSuggestionDto, VisitListItemDto} from '../services/visits/visit.models';

import {buildRouteDistanceMap} from '../services/visits/visitDisplayOrder';

import {formatDistanceKm} from '../services/visits/visitGeo';



type Props = {

  visible: boolean;

  suggestion: VisitGroupingSuggestionDto | null;

  visits: VisitListItemDto[];

  onCancel: () => void;

  onApply: (orderedVisitIds: string[]) => void;

};



function crimeLine(visit: VisitListItemDto): string {

  return `${visit.case?.crimeNumber ?? '—'} · ${visit.case?.stNumber ?? '—'}`;

}



export function SuggestedRouteSheet({

  visible,

  suggestion,

  visits,

  onCancel,

  onApply,

}: Props): React.JSX.Element {

  const initialOrder = suggestion?.suggestedVisitOrder?.map(id => String(id)) ?? [];

  const [orderedVisitIds, setOrderedVisitIds] = useState<string[]>(initialOrder);



  React.useEffect(() => {

    setOrderedVisitIds(suggestion?.suggestedVisitOrder?.map(id => String(id)) ?? []);

  }, [suggestion]);



  const visitById = useMemo(

    () =>

      new Map(

        visits

          .filter(visit => visit.id)

          .map(visit => [visit.id as string, visit]),

      ),

    [visits],

  );



  const distanceByVisitId = useMemo(

    () => buildRouteDistanceMap(orderedVisitIds, visits),

    [orderedVisitIds, visits],

  );



  const excludedVisits = (suggestion?.excluded ?? [])

    .map(item => visitById.get(String(item.visitId)))

    .filter((visit): visit is VisitListItemDto => !!visit);



  const canApply = orderedVisitIds.length > 0;



  const move = (index: number, direction: -1 | 1): void => {

    const target = index + direction;

    if (target < 0 || target >= orderedVisitIds.length) {

      return;

    }



    const next = [...orderedVisitIds];

    const [moved] = next.splice(index, 1);

    next.splice(target, 0, moved);

    setOrderedVisitIds(next);

  };



  return (

    <Modal visible={visible} animationType="slide" transparent onRequestClose={onCancel}>

      <View style={styles.overlay}>

        <View style={styles.sheet}>

          <Text style={styles.title}>Suggested route</Text>



          {suggestion?.message && orderedVisitIds.length === 0 ? (

            <Text style={styles.message}>{suggestion.message}</Text>

          ) : null}



          {excludedVisits.length > 0 ? (

            <View style={styles.warning} accessibilityRole="text">

              <Text style={styles.warningTitle}>

                {excludedVisits.length} visit(s) skipped — capture landmark before grouping

              </Text>

              {excludedVisits.map(visit => (

                <Text key={visit.id} style={styles.warningItem}>

                  {crimeLine(visit)}

                </Text>

              ))}

            </View>

          ) : null}



          <ScrollView style={styles.list}>

            {orderedVisitIds.map((visitId, index) => {

              const visit = visitById.get(visitId);

              if (!visit) {

                return null;

              }



              const distanceLabel = formatDistanceKm(distanceByVisitId.get(visitId));

              return (

                <View key={visitId} style={styles.row}>

                  <View style={styles.rowBody}>

                    <Text style={styles.crime}>{crimeLine(visit)}</Text>

                    {distanceLabel ? (

                      <Text style={styles.distance}>{distanceLabel}</Text>

                    ) : null}

                  </View>

                  <View style={styles.rowActions}>

                    <Pressable

                      onPress={() => move(index, -1)}

                      disabled={index === 0}

                      accessibilityRole="button"

                      accessibilityLabel="Move visit up">

                      <Text style={[styles.move, index === 0 ? styles.moveDisabled : null]}>

                        Up

                      </Text>

                    </Pressable>

                    <Pressable

                      onPress={() => move(index, 1)}

                      disabled={index === orderedVisitIds.length - 1}

                      accessibilityRole="button"

                      accessibilityLabel="Move visit down">

                      <Text

                        style={[

                          styles.move,

                          index === orderedVisitIds.length - 1 ? styles.moveDisabled : null,

                        ]}>

                        Down

                      </Text>

                    </Pressable>

                  </View>

                </View>

              );

            })}

          </ScrollView>



          <View style={styles.actions}>

            <Pressable

              style={styles.secondaryButton}

              onPress={onCancel}

              accessibilityRole="button">

              <Text style={styles.secondaryButtonText}>Cancel</Text>

            </Pressable>

            {canApply ? (

              <Pressable

                style={styles.primaryButton}

                onPress={() => onApply(orderedVisitIds)}

                accessibilityRole="button">

                <Text style={styles.primaryButtonText}>Apply route</Text>

              </Pressable>

            ) : null}

          </View>

        </View>

      </View>

    </Modal>

  );

}



const styles = StyleSheet.create({

  overlay: {

    flex: 1,

    justifyContent: 'flex-end',

    backgroundColor: 'rgba(16, 24, 40, 0.45)',

  },

  sheet: {

    maxHeight: '80%',

    backgroundColor: '#fff',

    borderTopLeftRadius: 16,

    borderTopRightRadius: 16,

    padding: 16,

  },

  title: {

    fontSize: 18,

    fontWeight: '600',

    color: '#101828',

    marginBottom: 12,

  },

  message: {

    fontSize: 14,

    color: '#475467',

    marginBottom: 12,

  },

  warning: {

    marginBottom: 12,

    padding: 10,

    backgroundColor: '#FFFAEB',

    borderLeftWidth: 4,

    borderLeftColor: '#B54708',

    borderRadius: 8,

  },

  warningTitle: {

    fontSize: 13,

    color: '#101828',

    marginBottom: 6,

  },

  warningItem: {

    fontSize: 12,

    color: '#475467',

  },

  list: {

    marginBottom: 12,

  },

  row: {

    flexDirection: 'row',

    alignItems: 'center',

    justifyContent: 'space-between',

    paddingVertical: 10,

    borderBottomWidth: 1,

    borderBottomColor: '#EAECF0',

  },

  rowBody: {

    flex: 1,

    paddingRight: 8,

  },

  crime: {

    fontSize: 14,

    fontWeight: '600',

    color: '#101828',

  },

  distance: {

    fontSize: 12,

    color: '#475467',

    marginTop: 4,

  },

  rowActions: {

    flexDirection: 'row',

    gap: 8,

  },

  move: {

    fontSize: 13,

    color: '#175CD3',

    fontWeight: '600',

  },

  moveDisabled: {

    color: '#98A2B3',

  },

  actions: {

    flexDirection: 'row',

    gap: 8,

  },

  secondaryButton: {

    flex: 1,

    paddingVertical: 12,

    borderRadius: 8,

    borderWidth: 1,

    borderColor: '#EAECF0',

    alignItems: 'center',

  },

  secondaryButtonText: {

    fontSize: 14,

    fontWeight: '600',

    color: '#101828',

  },

  primaryButton: {

    flex: 1,

    paddingVertical: 12,

    borderRadius: 8,

    backgroundColor: '#0D6E6E',

    alignItems: 'center',

  },

  primaryButtonText: {

    fontSize: 14,

    fontWeight: '600',

    color: '#fff',

  },

});


