import type {components} from '@midi-kaval/api-client';
import type {CaseSummaryDto} from '../cases/case.models';

// Hand-written rather than pulled from the generated client: the client's schema
// predates the places field (and the assigneeUserId/assigneeEmail/cancellationReason
// fields added earlier), since the "places to visit" feature is new.
export interface VisitPlaceDto {
  id: string;
  visitId: string;
  address: string;
  osmReference?: string | null;
  plannedLatitude?: number | null;
  plannedLongitude?: number | null;
  createdAtUtc: string;
  loggedLatitude?: number | null;
  loggedLongitude?: number | null;
  loggedAtUtc?: string | null;
  loggedByEmail?: string | null;
}

export type VisitListItemDto = Omit<
  components['schemas']['VisitListItemDto'],
  'case'
> & {
  case?: CaseSummaryDto | null;
  places?: VisitPlaceDto[];
};

export type VisitListResultDto = components['schemas']['VisitListResultDto'];

export type HandoffWhisperDto = components['schemas']['HandoffWhisperDto'];

export type VisitGroupingSuggestionDto =

  components['schemas']['VisitGroupingSuggestionDto'];

export type VisitGroupingClusterDto =

  components['schemas']['VisitGroupingClusterDto'];

export type VisitGroupingLegDto = components['schemas']['VisitGroupingLegDto'];

export type VisitGroupingExcludedDto =

  components['schemas']['VisitGroupingExcludedDto'];


