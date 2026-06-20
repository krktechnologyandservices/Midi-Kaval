import {useCallback, useState} from 'react';
import {CaseGpsDto} from '../cases/case.models';
import {VisitListItemDto} from './visit.models';
import {openGoogleMaps} from './visitNavigation';

export type CaseGpsUpdate = {
  gpsVerified: boolean;
  latitude?: number;
  longitude?: number;
  landmark?: string;
};

type Options = {
  onGpsVerified?: (caseId: string, gps: CaseGpsUpdate) => void;
};

export function useVisitNavigation(options: Options = {}) {
  const [captureVisible, setCaptureVisible] = useState(false);
  const [captureCaseId, setCaptureCaseId] = useState<string | null>(null);

  const navigateToVisit = useCallback(async (visit: VisitListItemDto): Promise<void> => {
    const caseSummary = visit.case;
    if (!caseSummary?.id) {
      return;
    }

    if (
      caseSummary.gpsVerified
      && caseSummary.latitude != null
      && caseSummary.longitude != null
    ) {
      await openGoogleMaps(Number(caseSummary.latitude), Number(caseSummary.longitude));
      return;
    }

    setCaptureCaseId(caseSummary.id);
    setCaptureVisible(true);
  }, []);

  const closeCapture = useCallback((): void => {
    setCaptureVisible(false);
    setCaptureCaseId(null);
  }, []);

  const handleCaptureSuccess = useCallback(
    async (gps: CaseGpsDto): Promise<void> => {
      const caseId = captureCaseId;
      setCaptureVisible(false);
      setCaptureCaseId(null);

      if (caseId) {
        options.onGpsVerified?.(caseId, {
          gpsVerified: true,
          latitude: gps.latitude != null ? Number(gps.latitude) : undefined,
          longitude: gps.longitude != null ? Number(gps.longitude) : undefined,
          landmark: gps.landmark ?? undefined,
        });
      }

      if (gps.latitude != null && gps.longitude != null) {
        await openGoogleMaps(Number(gps.latitude), Number(gps.longitude));
      }
    },
    [captureCaseId, options],
  );

  return {
    navigateToVisit,
    captureModalProps: {
      visible: captureVisible,
      caseId: captureCaseId,
      onClose: closeCapture,
      onSuccess: handleCaptureSuccess,
    },
  };
}

export function applyCaseGpsUpdate(
  items: VisitListItemDto[],
  caseId: string,
  gps: CaseGpsUpdate,
): VisitListItemDto[] {
  return items.map(item => {
    if (item.case?.id !== caseId) {
      return item;
    }

    return {
      ...item,
      case: {
        ...item.case,
        gpsVerified: gps.gpsVerified,
        latitude: gps.latitude ?? item.case.latitude,
        longitude: gps.longitude ?? item.case.longitude,
        landmark: gps.landmark ?? item.case.landmark,
      },
    };
  });
}
