import {useCallback, useEffect, useState} from 'react';
import {courtApiService} from '../services/court/CourtApiService';
import {buildCourtCountdownLabel} from '../utils/courtSittingUtils';

type Options = {
  enabled: boolean;
  refreshToken?: number;
};

export function useCourtCountdown({enabled, refreshToken}: Options): {
  label: string | null;
  refreshCourtCountdown: () => Promise<void>;
} {
  const [label, setLabel] = useState<string | null>(null);

  const refreshCourtCountdown = useCallback(async (): Promise<void> => {
    if (!enabled) {
      setLabel(null);
      return;
    }

    try {
      const items = await courtApiService.listUpcomingCourtSittings();
      // Items are ordered by scheduled date ascending. A stale sitting that already
      // happened but is still stuck in "Upcoming" status (nobody logged its outcome)
      // would otherwise always be items[0] and permanently hide today's/next sitting
      // from the banner. Prefer the earliest one that isn't overdue, falling back to
      // the overdue one only if that's genuinely all there is.
      const featured = items.find(item => !item.isPastDue) ?? items[0];
      setLabel(featured ? buildCourtCountdownLabel(featured) : null);
    } catch {
      setLabel(null);
    }
  }, [enabled]);

  useEffect(() => {
    void refreshCourtCountdown();
  }, [refreshCourtCountdown, refreshToken]);

  return {label, refreshCourtCountdown};
}
