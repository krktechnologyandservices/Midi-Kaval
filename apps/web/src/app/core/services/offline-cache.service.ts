import { Injectable } from '@angular/core';

const STORAGE_PREFIX = 'kaval_offline_';

export interface CacheEntry<T> {
  data: T;
  timestamp: string;
}

@Injectable({ providedIn: 'root' })
export class OfflineCacheService {
  set<T>(key: string, data: T): void {
    try {
      const entry: CacheEntry<T> = {
        data,
        timestamp: new Date().toISOString(),
      };
      localStorage.setItem(STORAGE_PREFIX + key, JSON.stringify(entry));
    } catch {
      // localStorage may be full or unavailable — silently ignore
    }
  }

  get<T>(key: string): CacheEntry<T> | null {
    try {
      const raw = localStorage.getItem(STORAGE_PREFIX + key);
      if (!raw) return null;
      return JSON.parse(raw) as CacheEntry<T>;
    } catch {
      return null;
    }
  }

  remove(key: string): void {
    try {
      localStorage.removeItem(STORAGE_PREFIX + key);
    } catch {
      // Silently ignore
    }
  }
}
