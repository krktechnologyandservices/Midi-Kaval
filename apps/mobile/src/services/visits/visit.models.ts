import type {components} from '@midi-kaval/api-client';
import type {CaseSummaryDto} from '../cases/case.models';

export type VisitListItemDto = Omit<
  components['schemas']['VisitListItemDto'],
  'case'
> & {
  case?: CaseSummaryDto | null;
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


