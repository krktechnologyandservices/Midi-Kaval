import React, {useCallback, useEffect, useMemo, useState} from 'react';



import {

  Alert,

  Pressable,

  RefreshControl,

  ScrollView,

  StyleSheet,

  Text,

  View,

} from 'react-native';



import {RouteProp, useNavigation, useRoute} from '@react-navigation/native';



import {NativeStackNavigationProp} from '@react-navigation/native-stack';



import {useAuth} from '../../context/AuthContext';



import {CommandStripCard} from '../../components/CommandStripCard';

import {CaptureLandmarkModal} from '../../components/CaptureLandmarkModal';

import {CommandStripSkeleton} from '../../components/CommandStripSkeleton';



import {CourtCountdownBanner} from '../../components/CourtCountdownBanner';
import {useCourtCountdown} from '../../hooks/useCourtCountdown';

import {SuggestedRouteSheet} from '../../components/SuggestedRouteSheet';

import {UnverifiedGpsGroupingBanner} from '../../components/UnverifiedGpsGroupingBanner';



import {TodayStackParamList} from '../../navigation/types';



import {

  isStale,

  readCache,

  writeCache,

} from '../../services/visits/commandStripCache';



import {visitApiService} from '../../services/visits/VisitApiService';

import {

  applyCaseGpsUpdate,

  useVisitNavigation,

} from '../../services/visits/useVisitNavigation';

import {

  applyDisplayOrder,

  buildRouteDistanceMap,

} from '../../services/visits/visitDisplayOrder';

import {mergeQueueWithVisits} from '../../services/sync/mergeQueueWithVisits';

import {readOfflineQueue} from '../../services/sync/offlineQueue';

import {resolveVisitSyncChip} from '../../services/sync/resolveVisitSyncChip';

import {flushOfflineQueue} from '../../services/sync/mobileSyncPushService';

import {useSyncOnForeground} from '../../services/sync/useSyncOnForeground';

import {QueuedMutation} from '../../services/sync/syncMutationTypes';

import {

  VisitGroupingSuggestionDto,

  VisitListItemDto,

  VisitPlaceDto,

} from '../../services/visits/visit.models';



type TodayHomeRoute = RouteProp<TodayStackParamList, 'TodayHome'>;



type TodayHomeNavigation = NativeStackNavigationProp<TodayStackParamList, 'TodayHome'>;



export function TodayScreen(): React.JSX.Element {

  const auth = useAuth();

  const navigation = useNavigation<TodayHomeNavigation>();

  const route = useRoute<TodayHomeRoute>();

  const [items, setItems] = useState<VisitListItemDto[]>([]);

  const [loading, setLoading] = useState(true);

  const [refreshing, setRefreshing] = useState(false);

  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const [groupingErrorMessage, setGroupingErrorMessage] = useState<string | null>(null);

  const [cacheStale, setCacheStale] = useState(false);

  const [initialLoadComplete, setInitialLoadComplete] = useState(false);

  const [startingVisitId, setStartingVisitId] = useState<string | null>(null);

  const [groupingLoading, setGroupingLoading] = useState(false);

  const [groupingSuggestion, setGroupingSuggestion] =

    useState<VisitGroupingSuggestionDto | null>(null);

  const [sheetVisible, setSheetVisible] = useState(false);

  const [customVisitOrder, setCustomVisitOrder] = useState<string[] | null>(null);

  const [routeGroupingActive, setRouteGroupingActive] = useState(false);

  const [bannerDismissed, setBannerDismissed] = useState(false);

  const [courtRefreshToken, setCourtRefreshToken] = useState(0);

  const {label: courtCountdownLabel} = useCourtCountdown({
    enabled: auth.isFieldRole,
    refreshToken: courtRefreshToken,
  });

  const [offlineQueue, setOfflineQueue] = useState<QueuedMutation[]>([]);



  const refreshOfflineQueue = useCallback(async (): Promise<QueuedMutation[]> => {

    const queue = await readOfflineQueue();

    setOfflineQueue(queue);

    return queue;

  }, []);



  const syncAfterQueueChange = useCallback(async (): Promise<void> => {

    const queueBeforeFlush = await readOfflineQueue();

    if (queueBeforeFlush.length) {

      setOfflineQueue(

        queueBeforeFlush.map(item => ({...item, syncStatus: 'pending' as const})),

      );

    }



    const result = await flushOfflineQueue();

    setOfflineQueue(result.remaining);

    setItems(prev => {

      const updated =
        result.appliedVisits.size > 0
          ? prev.map(item =>
              item.id && result.appliedVisits.has(item.id)
                ? result.appliedVisits.get(item.id)!
                : item,
            )
          : prev;

      void writeCache(mergeQueueWithVisits(updated, result.remaining), {

        customVisitOrder,

        routeGroupingActive,

      });

      return updated;

    });

  }, [customVisitOrder, routeGroupingActive]);



  useSyncOnForeground({

    enabled: auth.isFieldRole,

    onSynced: syncAfterQueueChange,

  });



  const {navigateToVisit, captureModalProps} = useVisitNavigation({

    onGpsVerified: (caseId, gps) => {

      setItems(prev => {

        const next = applyCaseGpsUpdate(prev, caseId, gps);

        void writeCache(next, {

          customVisitOrder,

          routeGroupingActive,

        });

        return next;

      });

    },

  });



  const mergedItems = useMemo(

    () => mergeQueueWithVisits(items, offlineQueue),

    [items, offlineQueue],

  );



  const displayItems = useMemo(

    () => applyDisplayOrder(mergedItems, customVisitOrder),

    [mergedItems, customVisitOrder],

  );



  const routeDistanceMap = useMemo(() => {

    if (!routeGroupingActive || !customVisitOrder?.length) {

      return new Map<string, number | null>();

    }



    return buildRouteDistanceMap(customVisitOrder, mergedItems);

  }, [customVisitOrder, mergedItems, routeGroupingActive]);



  const hasUnverifiedGps = items.some(item => item.case && !item.case.gpsVerified);



  const fetchVisits = useCallback(

    async (options?: {
      isRefresh?: boolean;
      hadCache?: boolean;
      clearGrouping?: boolean;
      customVisitOrderOverride?: string[] | null;
      routeGroupingActiveOverride?: boolean;
    }) => {

      if (!auth.isFieldRole) {

        setItems([]);

        setLoading(false);

        setRefreshing(false);

        setInitialLoadComplete(true);

        return;

      }



      try {

        const result = await visitApiService.listToday();

        const nextItems = result.items ?? [];

        setItems(nextItems);

        setErrorMessage(null);

        const queue = await refreshOfflineQueue();

        const mergedForCache = mergeQueueWithVisits(nextItems, queue);

        if (options?.clearGrouping) {

          setCustomVisitOrder(null);

          setRouteGroupingActive(false);

          await writeCache(mergedForCache, {

            customVisitOrder: null,

            routeGroupingActive: false,

          });

        } else {

          const nextOrder =
            options && 'customVisitOrderOverride' in options
              ? options.customVisitOrderOverride ?? null
              : customVisitOrder;
          const nextGroupingActive =
            options && 'routeGroupingActiveOverride' in options
              ? !!options.routeGroupingActiveOverride
              : routeGroupingActive;

          if (options && 'customVisitOrderOverride' in options) {
            setCustomVisitOrder(nextOrder);
          }
          if (options && 'routeGroupingActiveOverride' in options) {
            setRouteGroupingActive(nextGroupingActive);
          }

          await writeCache(mergedForCache, {

            customVisitOrder: nextOrder,

            routeGroupingActive: nextGroupingActive,

          });

        }



        setCacheStale(false);

      } catch (error) {

        setErrorMessage(visitApiService.extractErrorMessage(error));

        if (!options?.hadCache) {

          setItems([]);

        }

      } finally {

        setLoading(false);

        setRefreshing(false);

        setInitialLoadComplete(true);

      }

    },

    [auth.isFieldRole, customVisitOrder, routeGroupingActive, refreshOfflineQueue],

  );



  const loadFromCacheAndFetch = useCallback(async () => {

    if (!auth.isFieldRole) {

      setLoading(false);

      setInitialLoadComplete(true);

      return;

    }



    const cached = await readCache();

    await refreshOfflineQueue();

    const hadCache = !!cached?.items?.length;

    if (cached?.items) {

      setItems(cached.items);

      setCustomVisitOrder(cached.customVisitOrder ?? null);

      setRouteGroupingActive(!!cached.routeGroupingActive);

      setCacheStale(isStale(cached.fetchedAtUtc));

      setLoading(false);

    }



    await fetchVisits({
      hadCache,
      customVisitOrderOverride: cached?.customVisitOrder ?? null,
      routeGroupingActiveOverride: !!cached?.routeGroupingActive,
    });

  }, [auth.isFieldRole, fetchVisits, refreshOfflineQueue]);



  useEffect(() => {

    void loadFromCacheAndFetch();

  }, [loadFromCacheAndFetch]);



  useEffect(() => {

    if (route.params?.refreshToken) {

      setRefreshing(true);

      setCourtRefreshToken(current => current + 1);

      void fetchVisits({

        isRefresh: true,

        hadCache: items.length > 0,

      });

    }

  }, [route.params?.refreshToken, fetchVisits, items.length]);



  const onRefresh = (): void => {

    setRefreshing(true);

    setCourtRefreshToken(current => current + 1);

    void fetchVisits({

      isRefresh: true,

      hadCache: items.length > 0,

    });

  };



  const onRetry = (): void => {

    setLoading(items.length === 0);

    void fetchVisits({hadCache: items.length > 0});

  };



  const navigateToSyncQueue = (): void => {

    navigation.getParent()?.navigate('More', {screen: 'SyncQueue'});

  };



  const onGroupNearbyVisits = async (): Promise<void> => {

    if (groupingLoading) {

      return;

    }



    setGroupingErrorMessage(null);

    setGroupingLoading(true);

    try {

      const suggestion = await visitApiService.getTodayGroupingSuggestion();

      setGroupingSuggestion(suggestion);

      setSheetVisible(true);

    } catch (error) {

      setGroupingErrorMessage(visitApiService.extractErrorMessage(error));

    } finally {

      setGroupingLoading(false);

    }

  };



  const onApplyRoute = async (orderedVisitIds: string[]): Promise<void> => {

    setCustomVisitOrder(orderedVisitIds);

    setRouteGroupingActive(true);

    setSheetVisible(false);

    await writeCache(items, {

      customVisitOrder: orderedVisitIds,

      routeGroupingActive: true,

    });

  };



  const onStartVisit = async (visit: VisitListItemDto): Promise<void> => {

    if (!visit.id) {

      return;

    }



    if (visit.status === 'InProgress') {

      navigation.navigate('ActiveVisit', {visit});

      return;

    }



    if (startingVisitId) {

      return;

    }



    setStartingVisitId(visit.id);

    try {

      const started = await visitApiService.startVisit(visit);

      const queue = await refreshOfflineQueue();

      setItems(prev => {

        const next = prev.map(item => (item.id === visit.id ? started : item));

        void writeCache(mergeQueueWithVisits(next, queue), {

          customVisitOrder,

          routeGroupingActive,

        });

        return next;

      });

      navigation.navigate('ActiveVisit', {visit: started});

    } catch (error) {

      Alert.alert(visitApiService.extractErrorMessage(error));

    } finally {

      setStartingVisitId(null);

    }

  };



  const onPlaceLogged = (visitId: string, place: VisitPlaceDto): void => {

    setItems(prev => {

      const next = prev.map(item =>

        item.id === visitId

          ? {...item, places: (item.places ?? []).map(p => (p.id === place.id ? place : p))}

          : item,

      );

      void writeCache(mergeQueueWithVisits(next, offlineQueue), {

        customVisitOrder,

        routeGroupingActive,

      });

      return next;

    });

  };



  const showSkeleton = loading && items.length === 0;

  const showEmpty =

    initialLoadComplete && !loading && !refreshing && items.length === 0 && !errorMessage;



  return (

    <ScrollView

      style={styles.container}

      contentContainerStyle={styles.content}

      refreshControl={

        auth.isFieldRole ? (

          <RefreshControl refreshing={refreshing} onRefresh={onRefresh} />

        ) : undefined

      }>

      <Text style={styles.title}>Today</Text>



      {!auth.isFieldRole ? (

        <Text style={styles.subtitle}>Command Strip is for field workers only.</Text>

      ) : null}



      {auth.isFieldRole && items.length >= 2 ? (

        <Pressable

          onPress={() => void onGroupNearbyVisits()}

          disabled={groupingLoading}

          accessibilityRole="button"

          accessibilityLabel="Group nearby visits">

          <Text style={styles.groupButton}>

            {groupingLoading ? 'Grouping…' : 'Group nearby visits'}

          </Text>

        </Pressable>

      ) : null}



      {auth.isFieldRole ? (

        <Pressable

          onPress={() => navigation.navigate('CourtSchedule')}

          accessibilityRole="button"

          accessibilityLabel="Court this week">

          <Text style={styles.groupButton}>Court this week</Text>

        </Pressable>

      ) : null}

      {auth.isFieldRole ? (

        <Pressable

          onPress={() => navigation.navigate('UpcomingVisits')}

          accessibilityRole="button"

          accessibilityLabel="Upcoming visits">

          <Text style={styles.groupButton}>Upcoming visits</Text>

        </Pressable>

      ) : null}



      {auth.isFieldRole && hasUnverifiedGps && !bannerDismissed ? (

        <UnverifiedGpsGroupingBanner onDismiss={() => setBannerDismissed(true)} />

      ) : null}



      {auth.isFieldRole && cacheStale && (!initialLoadComplete || errorMessage) ? (

        <Text style={styles.staleBanner}>Showing saved visits — pull to refresh</Text>

      ) : null}



      <CourtCountdownBanner label={courtCountdownLabel} />



      {errorMessage ? (

        <View style={styles.errorBlock}>

          <Text style={styles.error}>{errorMessage}</Text>

          <Pressable onPress={onRetry} accessibilityRole="button">

            <Text style={styles.retryText}>Retry</Text>

          </Pressable>

        </View>

      ) : null}



      {groupingErrorMessage ? (

        <View style={styles.errorBlock}>

          <Text style={styles.error}>{groupingErrorMessage}</Text>

          <Pressable onPress={() => void onGroupNearbyVisits()} accessibilityRole="button">

            <Text style={styles.retryText}>Retry</Text>

          </Pressable>

        </View>

      ) : null}



      {showSkeleton ? <CommandStripSkeleton /> : null}



      {auth.isFieldRole

        ? displayItems.map((visit, index) => (

            <CommandStripCard

              key={visit.id ?? `${index}`}

              visit={visit}

              visitIndex={index}

              syncChip={resolveVisitSyncChip(visit.id, offlineQueue)}

              distanceKm={

                visit.id && routeGroupingActive

                  ? routeDistanceMap.get(visit.id) ?? null

                  : null

              }

              startVisitLoading={startingVisitId === visit.id}

              onNavigate={() => void navigateToVisit(visit)}

              onStartVisit={() => void onStartVisit(visit)}

              onSyncChipPress={navigateToSyncQueue}

              onPlaceLogged={onPlaceLogged}

              onOpenCase={() => {

                const caseId = visit.case?.id;

                if (!caseId) {

                  return;

                }



                navigation.getParent()?.navigate('Cases', {

                  screen: 'CaseDetailPlaceholder',

                  params: {caseId},

                });

              }}

            />

          ))

        : null}



      {showEmpty ? (

        <View style={styles.emptyState}>

          <Text style={styles.emptyTitle}>No visits scheduled for today</Text>

          <Text style={styles.emptySubtitle}>

            Pull to refresh when your coordinator schedules visits

          </Text>

        </View>

      ) : null}



      <CaptureLandmarkModal {...captureModalProps} />

      <SuggestedRouteSheet

        visible={sheetVisible}

        suggestion={groupingSuggestion}

        visits={items}

        onCancel={() => setSheetVisible(false)}

        onApply={orderedVisitIds => void onApplyRoute(orderedVisitIds)}

      />

    </ScrollView>

  );

}



const styles = StyleSheet.create({

  container: {

    flex: 1,

    backgroundColor: '#F8FAFC',

  },

  content: {

    padding: 16,

    paddingBottom: 32,

  },

  title: {

    fontSize: 22,

    fontWeight: '600',

    color: '#101828',

    marginBottom: 12,

  },

  groupButton: {

    fontSize: 14,

    fontWeight: '600',

    color: '#175CD3',

    marginBottom: 12,

  },

  subtitle: {

    fontSize: 14,

    color: '#475467',

    marginBottom: 12,

  },

  staleBanner: {

    fontSize: 13,

    color: '#475467',

    backgroundColor: '#F2F4F7',

    padding: 10,

    borderRadius: 8,

    marginBottom: 12,

  },

  errorBlock: {

    marginBottom: 12,

  },

  error: {

    color: '#B42318',

    marginBottom: 8,

  },

  retryText: {

    color: '#175CD3',

    fontWeight: '600',

  },

  emptyState: {

    marginTop: 24,

    padding: 16,

    alignItems: 'center',

  },

  emptyTitle: {

    fontSize: 16,

    fontWeight: '600',

    color: '#101828',

    textAlign: 'center',

  },

  emptySubtitle: {

    fontSize: 14,

    color: '#475467',

    marginTop: 8,

    textAlign: 'center',

  },

});


