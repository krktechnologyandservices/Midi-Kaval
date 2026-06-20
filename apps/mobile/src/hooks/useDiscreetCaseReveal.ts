import {useCallback, useState} from 'react';
import {authSessionService} from '../services/auth/AuthSessionService';
import {caseApiService} from '../services/cases/CaseApiService';
import {RevealCasePiiResponse} from '../services/cases/case.models';
import {DiscreetCaseHeader} from '../components/DiscreetHeader';

const OTP_WINDOW_MS = 5 * 60 * 1000;

export function useDiscreetCaseReveal(caseId: string, baseCase: DiscreetCaseHeader) {
  const [expanded, setExpanded] = useState(false);
  const [revealed, setRevealed] = useState<RevealCasePiiResponse | null>(null);
  const [expandLoading, setExpandLoading] = useState(false);
  const [stepUpVisible, setStepUpVisible] = useState(false);
  const [stepUpLoading, setStepUpLoading] = useState(false);
  const [stepUpError, setStepUpError] = useState<string | null>(null);

  const headerCase: DiscreetCaseHeader =
    expanded && revealed
      ? {
          ...baseCase,
          beneficiaryName: revealed.beneficiaryName,
          beneficiaryContact: revealed.beneficiaryContact ?? undefined,
          beneficiaryAge: revealed.beneficiaryAge ?? undefined,
        }
      : baseCase;

  const requestStepUpEmail = useCallback(async (): Promise<void> => {
    setStepUpLoading(true);
    setStepUpError(null);
    try {
      await authSessionService.stepUp();
    } catch (error) {
      setStepUpError(authSessionService.extractErrorMessage(error));
    } finally {
      setStepUpLoading(false);
    }
  }, []);

  const applyReveal = useCallback(async (): Promise<void> => {
    setExpandLoading(true);
    try {
      const data = await caseApiService.revealCasePii(caseId);
      setRevealed(data);
      setExpanded(true);
      setStepUpVisible(false);
      setStepUpError(null);
    } finally {
      setExpandLoading(false);
    }
  }, [caseId]);

  const openStepUpModal = useCallback((): void => {
    setStepUpError(null);
    setStepUpVisible(true);
    void requestStepUpEmail();
  }, [requestStepUpEmail]);

  const onExpandPress = useCallback(async (): Promise<void> => {
    if (expanded || expandLoading || !caseId.trim()) {
      return;
    }

    const lastVerified = authSessionService.getLastLoginOtpVerifiedAtUtc();
    const withinWindow =
      lastVerified != null &&
      Date.now() - new Date(lastVerified).getTime() <= OTP_WINDOW_MS;

    if (withinWindow) {
      try {
        await applyReveal();
      } catch {
        openStepUpModal();
      }
      return;
    }

    try {
      await applyReveal();
    } catch {
      openStepUpModal();
    }
  }, [applyReveal, caseId, expandLoading, expanded, openStepUpModal]);

  const onStepUpSubmit = useCallback(
    async (code: string): Promise<void> => {
      setStepUpLoading(true);
      setStepUpError(null);
      try {
        await authSessionService.verifyStepUp(code);
        await applyReveal();
      } catch (error) {
        setStepUpError(authSessionService.extractErrorMessage(error));
      } finally {
        setStepUpLoading(false);
      }
    },
    [applyReveal],
  );

  return {
    headerCase,
    expanded,
    expandLoading,
    stepUpVisible,
    stepUpLoading,
    stepUpError,
    onExpandPress,
    onStepUpSubmit,
    closeStepUp: () => setStepUpVisible(false),
  };
}
