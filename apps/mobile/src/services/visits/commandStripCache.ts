import AsyncStorage from '@react-native-async-storage/async-storage';

import {VisitListItemDto} from './visit.models';



const CACHE_KEY = '@midi-kaval/command-strip/v1';

const STALE_MS = 24 * 60 * 60 * 1000;



export interface CommandStripCachePayload {

  items: VisitListItemDto[];

  fetchedAtUtc: string;

  customVisitOrder?: string[] | null;

  routeGroupingActive?: boolean;

}



export interface WriteCacheOptions {

  customVisitOrder?: string[] | null;

  routeGroupingActive?: boolean;

}



export async function readCache(): Promise<CommandStripCachePayload | null> {

  const raw = await AsyncStorage.getItem(CACHE_KEY);

  if (!raw) {

    return null;

  }



  try {

    const parsed = JSON.parse(raw) as CommandStripCachePayload;

    if (!Array.isArray(parsed.items) || typeof parsed.fetchedAtUtc !== 'string') {

      return null;

    }



    return parsed;

  } catch {

    await AsyncStorage.removeItem(CACHE_KEY);

    return null;

  }

}



export async function clearCache(): Promise<void> {

  await AsyncStorage.removeItem(CACHE_KEY);

}



export async function writeCache(

  items: VisitListItemDto[],

  options?: WriteCacheOptions,

): Promise<void> {

  const existing = await readCache();

  const payload: CommandStripCachePayload = {

    items,

    fetchedAtUtc: new Date().toISOString(),

    customVisitOrder:

      options && 'customVisitOrder' in options

        ? options.customVisitOrder ?? null

        : existing?.customVisitOrder ?? null,

    routeGroupingActive:

      options && 'routeGroupingActive' in options

        ? options.routeGroupingActive ?? false

        : existing?.routeGroupingActive ?? false,

  };

  await AsyncStorage.setItem(CACHE_KEY, JSON.stringify(payload));

}



export function isStale(fetchedAtUtc: string): boolean {

  const fetchedAt = Date.parse(fetchedAtUtc);

  if (Number.isNaN(fetchedAt)) {

    return true;

  }



  return Date.now() - fetchedAt > STALE_MS;

}


