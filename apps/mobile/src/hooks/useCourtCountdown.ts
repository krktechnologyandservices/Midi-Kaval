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
      const first = items[0];
      setLabel(first ? buildCourtCountdownLabel(first) : null);
    } catch {
      setLabel(null);
    }
  }, [enabled]);

  useEffect(() => {
    void refreshCourtCountdown();
  }, [refreshCourtCountdown, refreshToken]);

  return {label, refreshCourtCountdown};
}
