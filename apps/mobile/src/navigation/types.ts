import {NavigatorScreenParams} from '@react-navigation/native';
import {VisitListItemDto} from '../services/visits/visit.models';

export type AuthStackParamList = {
  Login: {resetSuccess?: string} | undefined;
  Otp: undefined;
  SessionExpired: undefined;
  ForgotPassword: undefined;
  ResetPassword: {token?: string} | undefined;
};

export type MainTabParamList = {
  Today: NavigatorScreenParams<TodayStackParamList> | undefined;
  Cases: NavigatorScreenParams<CasesStackParamList> | undefined;
  More: NavigatorScreenParams<MoreStackParamList> | undefined;
};

export type MoreStackParamList = {
  MoreHome: undefined;
  SyncQueue: undefined;
  NotificationsList: undefined;
  TravelClaimsList: undefined;
  TravelClaimForm: {
    claimId?: string;
    localDraftKey?: string;
    mode: 'create' | 'edit' | 'view';
  };
};

export type TodayStackParamList = {
  TodayHome: {refreshToken?: number} | undefined;
  ActiveVisit: {visit: VisitListItemDto};
  CourtSchedule: undefined;
};

export type CasesStackParamList = {
  CasesList: undefined;
  CaseCreate: undefined;
  CaseDetailPlaceholder: {
    caseId: string;
    crimeNumber?: string;
    stNumber?: string;
    beneficiaryName?: string;
    currentStage?: string;
    matchedOn?: string;
    fromCreate?: boolean;
  };
};
