import DateTimePicker from '@react-native-community/datetimepicker';
import React, {useCallback, useEffect, useRef, useState} from 'react';
import {
  ActivityIndicator,
  Linking,
  Platform,
  Pressable,
  RefreshControl,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  View,
} from 'react-native';
import DocumentPicker, {types} from 'react-native-document-picker';
import {NativeStackScreenProps} from '@react-navigation/native-stack';
import {DiscreetExpandModal} from '../../components/DiscreetExpandModal';
import {DiscreetHeader} from '../../components/DiscreetHeader';
import {useDiscreetCaseReveal} from '../../hooks/useDiscreetCaseReveal';
import {CasesStackParamList} from '../../navigation/types';
import {attachmentApiService} from '../../services/attachments/AttachmentApiService';
import {caseApiService} from '../../services/cases/CaseApiService';
import {authSessionService} from '../../services/auth/AuthSessionService';
import {
  attachmentBasename,
  ALLOWED_ATTACHMENT_CONTENT_TYPES,
  CASE_NOTE_TYPES,
  CaseDetailDto,
  CaseNoteDto,
  CaseNoteType,
  COURT_SITTING_STATUSES,
  CourtSittingDto,
  CourtSittingStatus,
  FieldWorkerUserDto,
  INTERVENTION_DIRECTIONS,
  INTERVENTION_PRIORITIES,
  INTERVENTION_STATUSES,
  InterventionDirection,
  InterventionDto,
  InterventionPriority,
  InterventionStatus,
  MAX_ATTACHMENT_BYTES,
} from '../../services/cases/case.models';
import {isCourtSittingPastDue} from '../../utils/courtSittingUtils';

type Props = NativeStackScreenProps<CasesStackParamList, 'CaseDetailPlaceholder'>;

function defaultDueDate(): Date {
  const next = new Date();
  next.setDate(next.getDate() + 1);
  next.setHours(10, 0, 0, 0);
  return next;
}

function formatDateTime(date: Date): string {
  return date.toLocaleString(undefined, {
    dateStyle: 'medium',
    timeStyle: 'short',
  });
}

export function CaseDetailPlaceholderScreen({
  route,
}: Props): React.JSX.Element {
  const {caseId} = route.params;
  const scrollRef = useRef<ScrollView>(null);
  const notesSectionY = useRef(0);

  const [detail, setDetail] = useState<CaseDetailDto | null>(null);
  const [notes, setNotes] = useState<CaseNoteDto[]>([]);
  const [interventions, setInterventions] = useState<InterventionDto[]>([]);
  const [courtSittings, setCourtSittings] = useState<CourtSittingDto[]>([]);
  const [fieldWorkers, setFieldWorkers] = useState<FieldWorkerUserDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [notesLoading, setNotesLoading] = useState(true);
  const [interventionsLoading, setInterventionsLoading] = useState(true);
  const [courtSittingsLoading, setCourtSittingsLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [interventionSubmitting, setInterventionSubmitting] = useState(false);
  const [courtSittingSubmitting, setCourtSittingSubmitting] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [notesErrorMessage, setNotesErrorMessage] = useState<string | null>(null);
  const [interventionsErrorMessage, setInterventionsErrorMessage] = useState<string | null>(null);
  const [courtSittingsErrorMessage, setCourtSittingsErrorMessage] = useState<string | null>(null);
  const [formErrorMessage, setFormErrorMessage] = useState<string | null>(null);
  const [interventionFormErrorMessage, setInterventionFormErrorMessage] = useState<string | null>(null);
  const [courtSittingFormErrorMessage, setCourtSittingFormErrorMessage] = useState<string | null>(null);

  const [noteType, setNoteType] = useState<CaseNoteType>('General');
  const [bodyText, setBodyText] = useState('');
  const [actionRequired, setActionRequired] = useState(false);
  const [actionDueDate, setActionDueDate] = useState(defaultDueDate);
  const [showDuePicker, setShowDuePicker] = useState(Platform.OS === 'ios');
  const [pickedAttachment, setPickedAttachment] = useState<{
    uri: string;
    name: string;
    type: string;
    size: number;
  } | null>(null);

  const [interventionDirection, setInterventionDirection] = useState<InterventionDirection>('Needed');
  const [interventionCategory, setInterventionCategory] = useState('');
  const [interventionDescription, setInterventionDescription] = useState('');
  const [interventionPriority, setInterventionPriority] = useState<InterventionPriority>('Medium');
  const [assignedStaffUserId, setAssignedStaffUserId] = useState('');
  const [interventionDueDate, setInterventionDueDate] = useState(defaultDueDate);
  const [interventionProvidedDate, setInterventionProvidedDate] = useState(new Date());
  const [showInterventionDuePicker, setShowInterventionDuePicker] = useState(Platform.OS === 'ios');
  const [showInterventionProvidedPicker, setShowInterventionProvidedPicker] = useState(Platform.OS === 'ios');
  const [updatingInterventionId, setUpdatingInterventionId] = useState<string | null>(null);
  const [updateStatus, setUpdateStatus] = useState<InterventionStatus>('Open');
  const [updateOutcome, setUpdateOutcome] = useState('');

  const [courtSittingStatus, setCourtSittingStatus] = useState<CourtSittingStatus>('Upcoming');
  const [courtSittingCourtName, setCourtSittingCourtName] = useState('');
  const [courtSittingPurpose, setCourtSittingPurpose] = useState('');
  const [courtSittingNotes, setCourtSittingNotes] = useState('');
  const [courtSittingOutcome, setCourtSittingOutcome] = useState('');
  const [courtSittingScheduledDate, setCourtSittingScheduledDate] = useState(defaultDueDate);
  const [showCourtSittingScheduledPicker, setShowCourtSittingScheduledPicker] = useState(
    Platform.OS === 'ios',
  );
  const [updatingCourtSittingId, setUpdatingCourtSittingId] = useState<string | null>(null);
  const [updateCourtSittingStatus, setUpdateCourtSittingStatus] =
    useState<CourtSittingStatus>('Upcoming');
  const [updateCourtSittingCourtName, setUpdateCourtSittingCourtName] = useState('');
  const [updateCourtSittingPurpose, setUpdateCourtSittingPurpose] = useState('');
  const [updateCourtSittingNotes, setUpdateCourtSittingNotes] = useState('');
  const [updateCourtSittingOutcome, setUpdateCourtSittingOutcome] = useState('');
  const [updateCourtSittingScheduledDate, setUpdateCourtSittingScheduledDate] =
    useState(defaultDueDate);
  const [updateCourtSittingNextDate, setUpdateCourtSittingNextDate] = useState(defaultDueDate);
  const [updateCourtSittingHasNextDate, setUpdateCourtSittingHasNextDate] = useState(false);
  const [showUpdateCourtSittingScheduledPicker, setShowUpdateCourtSittingScheduledPicker] =
    useState(Platform.OS === 'ios');
  const [showUpdateCourtSittingNextPicker, setShowUpdateCourtSittingNextPicker] = useState(
    Platform.OS === 'ios',
  );

  const loadDetail = useCallback(async () => {
    try {
      const data = await caseApiService.getCaseDetail(caseId);
      setDetail(data);
      setErrorMessage(null);
    } catch (error) {
      setErrorMessage(caseApiService.extractErrorMessage(error));
    }
  }, [caseId]);

  const loadNotes = useCallback(async () => {
    setNotesLoading(true);
    try {
      const items = await caseApiService.listCaseNotes(caseId);
      setNotes(items);
      setNotesErrorMessage(null);
    } catch (error) {
      setNotesErrorMessage(caseApiService.extractErrorMessage(error));
    } finally {
      setNotesLoading(false);
    }
  }, [caseId]);

  const loadInterventions = useCallback(async () => {
    setInterventionsLoading(true);
    try {
      const items = await caseApiService.listInterventions(caseId);
      setInterventions(items);
      setInterventionsErrorMessage(null);
    } catch (error) {
      setInterventionsErrorMessage(caseApiService.extractErrorMessage(error));
    } finally {
      setInterventionsLoading(false);
    }
  }, [caseId]);

  const loadCourtSittings = useCallback(async () => {
    setCourtSittingsLoading(true);
    try {
      const items = await caseApiService.listCourtSittings(caseId);
      setCourtSittings(items);
      setCourtSittingsErrorMessage(null);
    } catch (error) {
      setCourtSittingsErrorMessage(caseApiService.extractErrorMessage(error));
    } finally {
      setCourtSittingsLoading(false);
    }
  }, [caseId]);

  const loadFieldWorkers = useCallback(async () => {
    try {
      const workers = await caseApiService.listFieldWorkers();
      setFieldWorkers(workers);
      setAssignedStaffUserId(current => current || workers[0]?.id || '');
    } catch {
      const user = authSessionService.getUser();
      if (user?.id) {
        setFieldWorkers([{id: user.id, email: user.email ?? user.id}]);
        setAssignedStaffUserId(current => current || user.id);
      }
    }
  }, []);

  const loadAll = useCallback(async () => {
    setLoading(true);
    await Promise.all([loadDetail(), loadNotes(), loadInterventions(), loadCourtSittings(), loadFieldWorkers()]);
    setLoading(false);
    setRefreshing(false);
  }, [loadDetail, loadNotes, loadInterventions, loadCourtSittings, loadFieldWorkers]);

  useEffect(() => {
    void loadAll();
  }, [loadAll]);

  const onRefresh = (): void => {
    setRefreshing(true);
    void loadAll();
  };

  const discreet = useDiscreetCaseReveal(caseId, {
    crimeNumber: detail?.crimeNumber,
    stNumber: detail?.stNumber,
    domicile: detail?.domicile,
    beneficiaryName: detail?.beneficiaryName,
    sensitivityLevel: detail?.sensitivityLevel,
  });

  const whisper = detail?.handoffWhisper;

  const scrollToNotes = (): void => {
    scrollRef.current?.scrollTo({y: notesSectionY.current, animated: true});
  };

  const pickAttachment = async (): Promise<void> => {
    try {
      const result = await DocumentPicker.pickSingle({
        type: [types.images, types.pdf],
        copyTo: 'cachesDirectory',
      });

      const mime = result.type ?? 'application/octet-stream';
      if (!ALLOWED_ATTACHMENT_CONTENT_TYPES.includes(mime as (typeof ALLOWED_ATTACHMENT_CONTENT_TYPES)[number])) {
        setFormErrorMessage('File type not allowed. Use JPEG, PNG, WebP, or PDF.');
        return;
      }

      const size = result.size;
      if (size == null || size <= 0) {
        setFormErrorMessage('Could not determine file size. Try another file.');
        return;
      }

      if (size > MAX_ATTACHMENT_BYTES) {
        setFormErrorMessage('File exceeds 10 MiB limit.');
        return;
      }

      setPickedAttachment({
        uri: result.fileCopyUri ?? result.uri,
        name: result.name ?? 'attachment',
        type: mime,
        size,
      });
      setFormErrorMessage(null);
    } catch (error) {
      if (!DocumentPicker.isCancel(error)) {
        setFormErrorMessage('Could not pick attachment.');
      }
    }
  };

  const submitNote = async (): Promise<void> => {
    const trimmed = bodyText.trim();
    if (!trimmed) {
      setFormErrorMessage('Note text is required.');
      return;
    }

    let actionDueAtUtc: string | null = null;
    if (actionRequired) {
      if (actionDueDate.getTime() <= Date.now()) {
        setFormErrorMessage('Action due date must be in the future.');
        return;
      }
      actionDueAtUtc = actionDueDate.toISOString();
    }

    setSubmitting(true);
    setFormErrorMessage(null);

    try {
      const created = await caseApiService.createCaseNote(caseId, {
        noteType,
        bodyText: trimmed,
        actionRequired: actionRequired || !!actionDueAtUtc,
        actionDueAtUtc,
      });

      if (pickedAttachment && created.id) {
        try {
          const presign = await attachmentApiService.presign({
            resourceType: 'CaseNote',
            resourceId: created.id,
            fileName: attachmentBasename(pickedAttachment.name),
            contentType: pickedAttachment.type,
            fileSizeBytes: pickedAttachment.size,
          });

          if (!presign.uploadUrl || !presign.attachmentId) {
            throw new Error('Presign failed');
          }

          const blob = await (await fetch(pickedAttachment.uri)).blob();
          await attachmentApiService.uploadToPresignedUrl(
            presign.uploadUrl,
            blob,
            presign.requiredHeaders ?? {
              'x-ms-blob-type': 'BlockBlob',
              'Content-Type': pickedAttachment.type,
            },
          );
          await attachmentApiService.confirm({attachmentId: presign.attachmentId});
        } catch (uploadError) {
          setFormErrorMessage(attachmentApiService.extractErrorMessage(uploadError));
        }
      }

      setBodyText('');
      setActionRequired(false);
      setActionDueDate(defaultDueDate());
      setPickedAttachment(null);
      await loadNotes();
    } catch (error) {
      setFormErrorMessage(caseApiService.extractErrorMessage(error));
    } finally {
      setSubmitting(false);
    }
  };

  const isInterventionOverdue = (item: InterventionDto): boolean =>
    item.direction === 'Needed'
    && item.status === 'Open'
    && !!item.dueAtUtc
    && new Date(item.dueAtUtc).getTime() < Date.now();

  const submitIntervention = async (): Promise<void> => {
    const categoryName = interventionCategory.trim();
    const description = interventionDescription.trim();

    if (!categoryName) {
      setInterventionFormErrorMessage('Category is required.');
      return;
    }

    if (!description) {
      setInterventionFormErrorMessage('Description is required.');
      return;
    }

    if (!assignedStaffUserId) {
      setInterventionFormErrorMessage('Assignee is required.');
      return;
    }

    let dueAtUtc: string | null = null;
    let providedAtUtc: string | null = null;

    if (interventionDirection === 'Needed') {
      if (interventionDueDate.getTime() <= Date.now()) {
        setInterventionFormErrorMessage('Due date must be in the future.');
        return;
      }
      dueAtUtc = interventionDueDate.toISOString();
    } else {
      providedAtUtc = interventionProvidedDate.toISOString();
    }

    setInterventionSubmitting(true);
    setInterventionFormErrorMessage(null);

    try {
      await caseApiService.createIntervention(caseId, {
        direction: interventionDirection,
        categoryName,
        description,
        priority: interventionPriority,
        status: 'Open',
        assignedStaffUserId,
        dueAtUtc,
        providedAtUtc,
      });

      setInterventionCategory('');
      setInterventionDescription('');
      setInterventionDirection('Needed');
      setInterventionPriority('Medium');
      setInterventionDueDate(defaultDueDate());
      setInterventionProvidedDate(new Date());
      await loadInterventions();
    } catch (error) {
      setInterventionFormErrorMessage(caseApiService.extractErrorMessage(error));
    } finally {
      setInterventionSubmitting(false);
    }
  };

  const startInterventionUpdate = (item: InterventionDto): void => {
    setUpdatingInterventionId(item.id ?? null);
    setUpdateStatus((item.status as InterventionStatus) ?? 'Open');
    setUpdateOutcome(item.outcome ?? '');
  };

  const cancelInterventionUpdate = (): void => {
    setUpdatingInterventionId(null);
  };

  const submitInterventionUpdate = async (item: InterventionDto): Promise<void> => {
    if (!item.id || updatingInterventionId !== item.id) {
      return;
    }

    if (
      (updateStatus === 'Completed' || updateStatus === 'Cancelled')
      && !updateOutcome.trim()
    ) {
      setInterventionFormErrorMessage('Outcome is required when status is Completed or Cancelled.');
      return;
    }

    setInterventionSubmitting(true);
    setInterventionFormErrorMessage(null);

    try {
      await caseApiService.updateIntervention(caseId, item.id, {
        status: updateStatus,
        outcome: updateOutcome.trim() || null,
        providedAtUtc:
          updateStatus === 'Completed' || updateStatus === 'Cancelled'
            ? new Date().toISOString()
            : undefined,
      });
      setUpdatingInterventionId(null);
      await loadInterventions();
    } catch (error) {
      setInterventionFormErrorMessage(caseApiService.extractErrorMessage(error));
    } finally {
      setInterventionSubmitting(false);
    }
  };

  const submitCourtSitting = async (): Promise<void> => {
    const courtName = courtSittingCourtName.trim();
    const purpose = courtSittingPurpose.trim();

    if (!courtName) {
      setCourtSittingFormErrorMessage('Court name is required.');
      return;
    }

    if (!purpose) {
      setCourtSittingFormErrorMessage('Purpose is required.');
      return;
    }

    if (
      courtSittingStatus === 'Upcoming'
      && courtSittingScheduledDate.getTime() <= Date.now()
    ) {
      setCourtSittingFormErrorMessage('Upcoming sittings must be scheduled in the future.');
      return;
    }

    if (courtSittingStatus === 'Attended' && !courtSittingOutcome.trim()) {
      setCourtSittingFormErrorMessage('Outcome is required when status is Attended.');
      return;
    }

    setCourtSittingSubmitting(true);
    setCourtSittingFormErrorMessage(null);

    try {
      await caseApiService.createCourtSitting(caseId, {
        scheduledAtUtc: courtSittingScheduledDate.toISOString(),
        courtName,
        purpose,
        status: courtSittingStatus,
        notes: courtSittingNotes.trim() || null,
        outcome: courtSittingOutcome.trim() || null,
      });

      setCourtSittingCourtName('');
      setCourtSittingPurpose('');
      setCourtSittingNotes('');
      setCourtSittingOutcome('');
      setCourtSittingStatus('Upcoming');
      setCourtSittingScheduledDate(defaultDueDate());
      await loadCourtSittings();
    } catch (error) {
      setCourtSittingFormErrorMessage(caseApiService.extractErrorMessage(error));
    } finally {
      setCourtSittingSubmitting(false);
    }
  };

  const startCourtSittingUpdate = (item: CourtSittingDto): void => {
    setUpdatingCourtSittingId(item.id ?? null);
    setUpdateCourtSittingStatus((item.status as CourtSittingStatus) ?? 'Upcoming');
    setUpdateCourtSittingCourtName(item.courtName ?? '');
    setUpdateCourtSittingPurpose(item.purpose ?? '');
    setUpdateCourtSittingNotes(item.notes ?? '');
    setUpdateCourtSittingOutcome(item.outcome ?? '');
    setUpdateCourtSittingScheduledDate(
      item.scheduledAtUtc ? new Date(item.scheduledAtUtc) : defaultDueDate(),
    );
    const hasNextDate = !!item.nextCourtAtUtc;
    setUpdateCourtSittingHasNextDate(hasNextDate);
    setUpdateCourtSittingNextDate(
      item.nextCourtAtUtc ? new Date(item.nextCourtAtUtc) : defaultDueDate(),
    );
    setShowUpdateCourtSittingNextPicker(Platform.OS === 'ios' && hasNextDate);
  };

  const cancelCourtSittingUpdate = (): void => {
    setUpdatingCourtSittingId(null);
  };

  const submitCourtSittingUpdate = async (item: CourtSittingDto): Promise<void> => {
    if (!item.id || updatingCourtSittingId !== item.id) {
      return;
    }

    if (
      updateCourtSittingStatus === 'Upcoming'
      && updateCourtSittingScheduledDate.getTime() <= Date.now()
    ) {
      setCourtSittingFormErrorMessage('Upcoming sittings must be scheduled in the future.');
      return;
    }

    if (updateCourtSittingStatus === 'Attended' && !updateCourtSittingOutcome.trim()) {
      setCourtSittingFormErrorMessage('Outcome is required when status is Attended.');
      return;
    }

    setCourtSittingSubmitting(true);
    setCourtSittingFormErrorMessage(null);

    try {
      const request: Parameters<typeof caseApiService.updateCourtSitting>[2] = {
        status: updateCourtSittingStatus,
        scheduledAtUtc: updateCourtSittingScheduledDate.toISOString(),
        courtName: updateCourtSittingCourtName.trim(),
        purpose: updateCourtSittingPurpose.trim(),
        notes: updateCourtSittingNotes.trim() || null,
        outcome: updateCourtSittingOutcome.trim() || null,
      };

      if (updateCourtSittingStatus === 'Postponed' && updateCourtSittingHasNextDate) {
        request.nextCourtAtUtc = updateCourtSittingNextDate.toISOString();
      }

      await caseApiService.updateCourtSitting(caseId, item.id, request);
      setUpdatingCourtSittingId(null);
      await loadCourtSittings();
    } catch (error) {
      setCourtSittingFormErrorMessage(caseApiService.extractErrorMessage(error));
    } finally {
      setCourtSittingSubmitting(false);
    }
  };

  const openAttachment = async (attachmentId: string | undefined): Promise<void> => {
    if (!attachmentId) {
      return;
    }

    try {
      const result = await attachmentApiService.getDownloadUrl(attachmentId);
      if (result.downloadUrl) {
        await Linking.openURL(result.downloadUrl);
      }
    } catch (error) {
      setFormErrorMessage(attachmentApiService.extractDownloadErrorMessage(error));
    }
  };

  const authorLabel = (note: CaseNoteDto): string =>
    note.authorEmail ?? (note.authorUserId ? `${note.authorUserId.slice(0, 8)}…` : 'Unknown');

  return (
    <ScrollView
      ref={scrollRef}
      style={styles.container}
      contentContainerStyle={styles.content}
      refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} />}>
      <Text style={styles.title}>Case detail</Text>
      <Text style={styles.subtitle}>Case ID: {caseId}</Text>

      {loading ? <ActivityIndicator /> : null}
      {errorMessage ? (
        <>
          <Text style={styles.error}>{errorMessage}</Text>
          <Pressable style={styles.retryButton} onPress={() => void loadAll()}>
            <Text style={styles.retryText}>Retry</Text>
          </Pressable>
        </>
      ) : null}

      {detail ? (
        <>
          <View style={styles.headerCard}>
            <DiscreetHeader
              caseInfo={discreet.headerCase}
              expanded={discreet.expanded}
              expandLoading={discreet.expandLoading}
              onExpandPress={() => void discreet.onExpandPress()}
            />
          </View>
          <Text>Stage: {detail.currentStage}</Text>

          {whisper ? (
            <View style={styles.whisper} accessibilityLabel="Handoff summary">
              <Text style={styles.whisperLine} numberOfLines={1} ellipsizeMode="tail">
                Prior actions: {whisper.priorActions}
              </Text>
              <Text style={styles.whisperLine} numberOfLines={1} ellipsizeMode="tail">
                Open items: {whisper.openItems}
              </Text>
              <Text style={styles.whisperLine} numberOfLines={1} ellipsizeMode="tail">
                Next visit: {whisper.nextVisitPurpose}
              </Text>
              <Pressable onPress={scrollToNotes} accessibilityRole="button" accessibilityLabel="View full timeline">
                <Text style={styles.timelineLink}>View full timeline</Text>
              </Pressable>
            </View>
          ) : null}
        </>
      ) : null}

      <View
        onLayout={event => {
          notesSectionY.current = event.nativeEvent.layout.y;
        }}
        style={styles.notesSection}
        accessibilityLabel="Case notes timeline">
        <Text style={styles.sectionTitle}>Notes timeline</Text>

        {notesLoading ? <ActivityIndicator /> : null}
        {notesErrorMessage ? (
          <>
            <Text style={styles.error}>{notesErrorMessage}</Text>
            <Pressable onPress={() => void loadNotes()}>
              <Text style={styles.retryText}>Retry notes</Text>
            </Pressable>
          </>
        ) : null}

        {!notesLoading && notes.length === 0 ? (
          <Text style={styles.emptyState}>No notes yet. Add the first note below.</Text>
        ) : null}

        {notes.map(note => (
          <View key={note.id} style={styles.noteItem}>
            <View style={styles.noteHeader}>
              <Text style={[styles.badge, badgeStyle(note.noteType)]}>{note.noteType}</Text>
              <Text style={styles.noteMeta}>
                {authorLabel(note)} · {note.createdAtUtc ? new Date(note.createdAtUtc).toLocaleString() : ''}
              </Text>
            </View>
            <Text style={styles.noteBody}>{note.bodyText}</Text>
            {note.actionRequired ? (
              <Text style={styles.actionRequired}>
                Action required
                {note.actionDueAtUtc
                  ? ` · due ${new Date(note.actionDueAtUtc).toLocaleString()}`
                  : ''}
              </Text>
            ) : null}
            {(note.attachments ?? []).map(attachment => (
              <Pressable
                key={attachment.id}
                style={styles.attachmentChip}
                onPress={() => void openAttachment(attachment.id)}>
                <Text style={styles.attachmentText}>{attachment.originalFileName}</Text>
              </Pressable>
            ))}
          </View>
        ))}

        <Text style={styles.formTitle}>Add note</Text>
        {formErrorMessage ? <Text style={styles.error}>{formErrorMessage}</Text> : null}

        <View style={styles.typeRow}>
          {CASE_NOTE_TYPES.map(type => (
            <Pressable
              key={type}
              style={[styles.typeChip, noteType === type ? styles.typeChipSelected : null]}
              onPress={() => setNoteType(type)}>
              <Text style={noteType === type ? styles.typeChipTextSelected : styles.typeChipText}>
                {type}
              </Text>
            </Pressable>
          ))}
        </View>

        <TextInput
          style={styles.textInput}
          multiline
          maxLength={4000}
          value={bodyText}
          onChangeText={setBodyText}
          placeholder="Note text"
          accessibilityLabel="Note text"
        />

        <Pressable
          style={styles.checkboxRow}
          onPress={() => setActionRequired(current => !current)}>
          <Text>{actionRequired ? '☑' : '☐'} Action required</Text>
        </Pressable>

        {actionRequired ? (
          <>
            {Platform.OS === 'android' ? (
              <Pressable style={styles.dateButton} onPress={() => setShowDuePicker(true)}>
                <Text style={styles.dateButtonText}>{formatDateTime(actionDueDate)}</Text>
              </Pressable>
            ) : (
              <Text style={styles.datePreview}>{formatDateTime(actionDueDate)}</Text>
            )}
            {showDuePicker ? (
              <DateTimePicker
                value={actionDueDate}
                mode="datetime"
                minimumDate={new Date()}
                onChange={(_event, date) => {
                  if (Platform.OS === 'android') {
                    setShowDuePicker(false);
                  }
                  if (date) {
                    setActionDueDate(date);
                  }
                }}
              />
            ) : null}
          </>
        ) : null}

        <Pressable style={styles.secondaryButton} onPress={() => void pickAttachment()}>
          <Text style={styles.secondaryButtonText}>
            {pickedAttachment ? pickedAttachment.name : 'Pick attachment (optional)'}
          </Text>
        </Pressable>

        <Pressable
          style={[styles.primaryButton, submitting ? styles.primaryButtonDisabled : null]}
          disabled={submitting}
          accessibilityRole="button"
          accessibilityLabel="Add case note"
          onPress={() => void submitNote()}>
          <Text style={styles.primaryButtonText}>{submitting ? 'Saving…' : 'Add note'}</Text>
        </Pressable>
      </View>

      <View style={styles.notesSection} accessibilityLabel="Case interventions">
        <Text style={styles.sectionTitle}>Interventions</Text>

        {interventionsLoading ? <ActivityIndicator /> : null}
        {interventionsErrorMessage ? (
          <>
            <Text style={styles.error}>{interventionsErrorMessage}</Text>
            <Pressable onPress={() => void loadInterventions()}>
              <Text style={styles.retryText}>Retry interventions</Text>
            </Pressable>
          </>
        ) : null}

        {!interventionsLoading && interventions.length === 0 ? (
          <Text style={styles.emptyState}>No interventions yet.</Text>
        ) : null}

        {interventions.map(item => (
          <View
            key={item.id}
            style={[
              styles.noteItem,
              isInterventionOverdue(item) ? styles.interventionOverdue : null,
            ]}>
            <View style={styles.noteHeader}>
              <Text style={[styles.badge, styles.badgeIntervention]}>{item.direction}</Text>
              <Text style={[styles.badge, styles.badgeGeneral]}>{item.status}</Text>
              <Text style={[styles.badge, styles.badgeVisit]}>{item.priority}</Text>
              {isInterventionOverdue(item) ? (
                <Text style={[styles.badge, styles.badgeOverdue]}>Overdue</Text>
              ) : null}
            </View>
            <Text style={styles.noteBody}>{item.categoryName}</Text>
            <Text style={styles.noteMeta}>{item.description}</Text>
            <Text style={styles.noteMeta}>
              Assignee: {item.assignedStaffEmail ?? item.assignedStaffUserId}
              {item.dueAtUtc ? ` · Due ${new Date(item.dueAtUtc).toLocaleString()}` : ''}
              {item.providedAtUtc
                ? ` · Provided ${new Date(item.providedAtUtc).toLocaleString()}`
                : ''}
            </Text>
            {item.outcome ? <Text style={styles.noteMeta}>Outcome: {item.outcome}</Text> : null}

            {updatingInterventionId === item.id ? (
              <>
                <View style={styles.typeRow}>
                  {INTERVENTION_STATUSES.map(status => (
                    <Pressable
                      key={status}
                      style={[
                        styles.typeChip,
                        updateStatus === status ? styles.typeChipSelected : null,
                      ]}
                      onPress={() => setUpdateStatus(status)}>
                      <Text
                        style={
                          updateStatus === status
                            ? styles.typeChipTextSelected
                            : styles.typeChipText
                        }>
                        {status}
                      </Text>
                    </Pressable>
                  ))}
                </View>
                <TextInput
                  style={styles.textInput}
                  multiline
                  maxLength={2000}
                  value={updateOutcome}
                  onChangeText={setUpdateOutcome}
                  placeholder="Outcome"
                  accessibilityLabel="Intervention outcome"
                />
                <Pressable
                  style={[
                    styles.primaryButton,
                    interventionSubmitting ? styles.primaryButtonDisabled : null,
                  ]}
                  disabled={interventionSubmitting}
                  accessibilityRole="button"
                  accessibilityLabel="Save intervention update"
                  onPress={() => void submitInterventionUpdate(item)}>
                  <Text style={styles.primaryButtonText}>
                    {interventionSubmitting ? 'Saving…' : 'Save'}
                  </Text>
                </Pressable>
                <Pressable onPress={cancelInterventionUpdate}>
                  <Text style={styles.retryText}>Cancel</Text>
                </Pressable>
              </>
            ) : (
              <Pressable onPress={() => startInterventionUpdate(item)}>
                <Text style={styles.retryText}>Update</Text>
              </Pressable>
            )}
          </View>
        ))}

        <Text style={styles.formTitle}>Add intervention</Text>
        {interventionFormErrorMessage ? (
          <Text style={styles.error}>{interventionFormErrorMessage}</Text>
        ) : null}

        <View style={styles.typeRow}>
          {INTERVENTION_DIRECTIONS.map(direction => (
            <Pressable
              key={direction}
              style={[
                styles.typeChip,
                interventionDirection === direction ? styles.typeChipSelected : null,
              ]}
              onPress={() => setInterventionDirection(direction)}>
              <Text
                style={
                  interventionDirection === direction
                    ? styles.typeChipTextSelected
                    : styles.typeChipText
                }>
                {direction}
              </Text>
            </Pressable>
          ))}
        </View>

        <TextInput
          style={styles.textInput}
          maxLength={128}
          value={interventionCategory}
          onChangeText={setInterventionCategory}
          placeholder="Category"
          accessibilityLabel="Intervention category"
        />
        <TextInput
          style={styles.textInput}
          multiline
          maxLength={4000}
          value={interventionDescription}
          onChangeText={setInterventionDescription}
          placeholder="Description"
          accessibilityLabel="Intervention description"
        />

        <View style={styles.typeRow}>
          {INTERVENTION_PRIORITIES.map(priority => (
            <Pressable
              key={priority}
              style={[
                styles.typeChip,
                interventionPriority === priority ? styles.typeChipSelected : null,
              ]}
              onPress={() => setInterventionPriority(priority)}>
              <Text
                style={
                  interventionPriority === priority
                    ? styles.typeChipTextSelected
                    : styles.typeChipText
                }>
                {priority}
              </Text>
            </Pressable>
          ))}
        </View>

        <View style={styles.typeRow}>
          {fieldWorkers.map(worker => (
            <Pressable
              key={worker.id}
              style={[
                styles.typeChip,
                assignedStaffUserId === worker.id ? styles.typeChipSelected : null,
              ]}
              onPress={() => worker.id && setAssignedStaffUserId(worker.id)}>
              <Text
                style={
                  assignedStaffUserId === worker.id
                    ? styles.typeChipTextSelected
                    : styles.typeChipText
                }>
                {worker.email ?? worker.id}
              </Text>
            </Pressable>
          ))}
        </View>

        {interventionDirection === 'Needed' ? (
          <>
            {Platform.OS === 'android' ? (
              <Pressable
                style={styles.dateButton}
                onPress={() => setShowInterventionDuePicker(true)}>
                <Text style={styles.dateButtonText}>{formatDateTime(interventionDueDate)}</Text>
              </Pressable>
            ) : (
              <Text style={styles.datePreview}>{formatDateTime(interventionDueDate)}</Text>
            )}
            {showInterventionDuePicker ? (
              <DateTimePicker
                value={interventionDueDate}
                mode="datetime"
                minimumDate={new Date()}
                onChange={(_event, date) => {
                  if (Platform.OS === 'android') {
                    setShowInterventionDuePicker(false);
                  }
                  if (date) {
                    setInterventionDueDate(date);
                  }
                }}
              />
            ) : null}
          </>
        ) : (
          <>
            {Platform.OS === 'android' ? (
              <Pressable
                style={styles.dateButton}
                onPress={() => setShowInterventionProvidedPicker(true)}>
                <Text style={styles.dateButtonText}>
                  {formatDateTime(interventionProvidedDate)}
                </Text>
              </Pressable>
            ) : (
              <Text style={styles.datePreview}>{formatDateTime(interventionProvidedDate)}</Text>
            )}
            {showInterventionProvidedPicker ? (
              <DateTimePicker
                value={interventionProvidedDate}
                mode="datetime"
                onChange={(_event, date) => {
                  if (Platform.OS === 'android') {
                    setShowInterventionProvidedPicker(false);
                  }
                  if (date) {
                    setInterventionProvidedDate(date);
                  }
                }}
              />
            ) : null}
          </>
        )}

        <Pressable
          style={[
            styles.primaryButton,
            interventionSubmitting ? styles.primaryButtonDisabled : null,
          ]}
          disabled={interventionSubmitting}
          accessibilityRole="button"
          accessibilityLabel="Add intervention"
          onPress={() => void submitIntervention()}>
          <Text style={styles.primaryButtonText}>
            {interventionSubmitting ? 'Saving…' : 'Add intervention'}
          </Text>
        </Pressable>
      </View>

      <View style={styles.notesSection} accessibilityLabel="Case court sittings">
        <Text style={styles.sectionTitle}>Court sittings</Text>

        {courtSittingsLoading ? <ActivityIndicator /> : null}
        {courtSittingsErrorMessage ? (
          <>
            <Text style={styles.error}>{courtSittingsErrorMessage}</Text>
            <Pressable onPress={() => void loadCourtSittings()}>
              <Text style={styles.retryText}>Retry court sittings</Text>
            </Pressable>
          </>
        ) : null}

        {!courtSittingsLoading && courtSittings.length === 0 ? (
          <Text style={styles.emptyState}>No court sittings yet.</Text>
        ) : null}

        {courtSittings.map(item => (
          <View
            key={item.id}
            style={[
              styles.noteItem,
              isCourtSittingPastDue(item) ? styles.courtSittingPastDue : null,
            ]}>
            <View style={styles.noteHeader}>
              <Text style={[styles.badge, styles.badgeCourt]}>{item.status}</Text>
              {isCourtSittingPastDue(item) ? (
                <Text style={[styles.badge, styles.badgeOverdue]}>Overdue</Text>
              ) : null}
            </View>
            <Text style={styles.noteBody}>{item.courtName}</Text>
            <Text style={styles.noteMeta}>{item.purpose}</Text>
            <Text style={styles.noteMeta}>
              Scheduled {item.scheduledAtUtc ? new Date(item.scheduledAtUtc).toLocaleString() : ''}
              {item.nextCourtAtUtc
                ? ` · Next court ${new Date(item.nextCourtAtUtc).toLocaleString()}`
                : ''}
            </Text>
            {item.notes ? <Text style={styles.noteMeta}>Notes: {item.notes}</Text> : null}
            {item.outcome ? <Text style={styles.noteMeta}>Outcome: {item.outcome}</Text> : null}

            {updatingCourtSittingId === item.id ? (
              <>
                <View style={styles.typeRow}>
                  {COURT_SITTING_STATUSES.map(status => (
                    <Pressable
                      key={status}
                      style={[
                        styles.typeChip,
                        updateCourtSittingStatus === status ? styles.typeChipSelected : null,
                      ]}
                      onPress={() => setUpdateCourtSittingStatus(status)}>
                      <Text
                        style={
                          updateCourtSittingStatus === status
                            ? styles.typeChipTextSelected
                            : styles.typeChipText
                        }>
                        {status}
                      </Text>
                    </Pressable>
                  ))}
                </View>
                <TextInput
                  style={styles.textInput}
                  maxLength={256}
                  value={updateCourtSittingCourtName}
                  onChangeText={setUpdateCourtSittingCourtName}
                  placeholder="Court name"
                  accessibilityLabel="Court name"
                />
                <TextInput
                  style={styles.textInput}
                  multiline
                  maxLength={512}
                  value={updateCourtSittingPurpose}
                  onChangeText={setUpdateCourtSittingPurpose}
                  placeholder="Purpose"
                  accessibilityLabel="Court sitting purpose"
                />
                {Platform.OS === 'android' ? (
                  <Pressable
                    style={styles.dateButton}
                    onPress={() => setShowUpdateCourtSittingScheduledPicker(true)}>
                    <Text style={styles.dateButtonText}>
                      {formatDateTime(updateCourtSittingScheduledDate)}
                    </Text>
                  </Pressable>
                ) : (
                  <Text style={styles.datePreview}>
                    {formatDateTime(updateCourtSittingScheduledDate)}
                  </Text>
                )}
                {showUpdateCourtSittingScheduledPicker ? (
                  <DateTimePicker
                    value={updateCourtSittingScheduledDate}
                    mode="datetime"
                    onChange={(_event, date) => {
                      if (Platform.OS === 'android') {
                        setShowUpdateCourtSittingScheduledPicker(false);
                      }
                      if (date) {
                        setUpdateCourtSittingScheduledDate(date);
                      }
                    }}
                  />
                ) : null}
                <TextInput
                  style={styles.textInput}
                  multiline
                  maxLength={2000}
                  value={updateCourtSittingNotes}
                  onChangeText={setUpdateCourtSittingNotes}
                  placeholder="Notes"
                  accessibilityLabel="Court sitting notes"
                />
                <TextInput
                  style={styles.textInput}
                  multiline
                  maxLength={2000}
                  value={updateCourtSittingOutcome}
                  onChangeText={setUpdateCourtSittingOutcome}
                  placeholder="Outcome"
                  accessibilityLabel="Court sitting outcome"
                />
                {updateCourtSittingStatus === 'Postponed' ? (
                  <>
                    <Pressable
                      style={styles.typeChip}
                      onPress={() => {
                        const next = !updateCourtSittingHasNextDate;
                        setUpdateCourtSittingHasNextDate(next);
                        setShowUpdateCourtSittingNextPicker(Platform.OS === 'ios' && next);
                      }}>
                      <Text style={styles.typeChipText}>
                        {updateCourtSittingHasNextDate
                          ? 'Next court date set'
                          : 'Set next court date (optional)'}
                      </Text>
                    </Pressable>
                    {updateCourtSittingHasNextDate ? (
                      <>
                        {Platform.OS === 'android' ? (
                          <Pressable
                            style={styles.dateButton}
                            onPress={() => setShowUpdateCourtSittingNextPicker(true)}>
                            <Text style={styles.dateButtonText}>
                              Next court: {formatDateTime(updateCourtSittingNextDate)}
                            </Text>
                          </Pressable>
                        ) : (
                          <Text style={styles.datePreview}>
                            Next court: {formatDateTime(updateCourtSittingNextDate)}
                          </Text>
                        )}
                        {showUpdateCourtSittingNextPicker ? (
                          <DateTimePicker
                            value={updateCourtSittingNextDate}
                            mode="datetime"
                            onChange={(_event, date) => {
                              if (Platform.OS === 'android') {
                                setShowUpdateCourtSittingNextPicker(false);
                              }
                              if (date) {
                                setUpdateCourtSittingNextDate(date);
                              }
                            }}
                          />
                        ) : null}
                      </>
                    ) : null}
                  </>
                ) : null}
                <Pressable
                  style={[
                    styles.primaryButton,
                    courtSittingSubmitting ? styles.primaryButtonDisabled : null,
                  ]}
                  disabled={courtSittingSubmitting}
                  onPress={() => void submitCourtSittingUpdate(item)}>
                  <Text style={styles.primaryButtonText}>
                    {courtSittingSubmitting ? 'Saving…' : 'Save'}
                  </Text>
                </Pressable>
                <Pressable onPress={cancelCourtSittingUpdate}>
                  <Text style={styles.retryText}>Cancel</Text>
                </Pressable>
              </>
            ) : (
              <Pressable onPress={() => startCourtSittingUpdate(item)}>
                <Text style={styles.retryText}>Update</Text>
              </Pressable>
            )}
          </View>
        ))}

        <Text style={styles.formTitle}>Add court sitting</Text>
        {courtSittingFormErrorMessage ? (
          <Text style={styles.error}>{courtSittingFormErrorMessage}</Text>
        ) : null}

        <View style={styles.typeRow}>
          {COURT_SITTING_STATUSES.map(status => (
            <Pressable
              key={status}
              style={[
                styles.typeChip,
                courtSittingStatus === status ? styles.typeChipSelected : null,
              ]}
              onPress={() => setCourtSittingStatus(status)}>
              <Text
                style={
                  courtSittingStatus === status
                    ? styles.typeChipTextSelected
                    : styles.typeChipText
                }>
                {status}
              </Text>
            </Pressable>
          ))}
        </View>

        <TextInput
          style={styles.textInput}
          maxLength={256}
          value={courtSittingCourtName}
          onChangeText={setCourtSittingCourtName}
          placeholder="Court name"
          accessibilityLabel="Court name"
        />
        <TextInput
          style={styles.textInput}
          multiline
          maxLength={512}
          value={courtSittingPurpose}
          onChangeText={setCourtSittingPurpose}
          placeholder="Purpose"
          accessibilityLabel="Court sitting purpose"
        />

        {Platform.OS === 'android' ? (
          <Pressable
            style={styles.dateButton}
            onPress={() => setShowCourtSittingScheduledPicker(true)}>
            <Text style={styles.dateButtonText}>{formatDateTime(courtSittingScheduledDate)}</Text>
          </Pressable>
        ) : (
          <Text style={styles.datePreview}>{formatDateTime(courtSittingScheduledDate)}</Text>
        )}
        {showCourtSittingScheduledPicker ? (
          <DateTimePicker
            value={courtSittingScheduledDate}
            mode="datetime"
            minimumDate={courtSittingStatus === 'Upcoming' ? new Date() : undefined}
            onChange={(_event, date) => {
              if (Platform.OS === 'android') {
                setShowCourtSittingScheduledPicker(false);
              }
              if (date) {
                setCourtSittingScheduledDate(date);
              }
            }}
          />
        ) : null}

        <TextInput
          style={styles.textInput}
          multiline
          maxLength={2000}
          value={courtSittingNotes}
          onChangeText={setCourtSittingNotes}
          placeholder="Notes (optional)"
          accessibilityLabel="Court sitting notes"
        />

        {courtSittingStatus === 'Attended' ? (
          <TextInput
            style={styles.textInput}
            multiline
            maxLength={2000}
            value={courtSittingOutcome}
            onChangeText={setCourtSittingOutcome}
            placeholder="Outcome"
            accessibilityLabel="Court sitting outcome"
          />
        ) : null}

        <Pressable
          style={[
            styles.primaryButton,
            courtSittingSubmitting ? styles.primaryButtonDisabled : null,
          ]}
          disabled={courtSittingSubmitting}
          accessibilityRole="button"
          accessibilityLabel="Add court sitting"
          onPress={() => void submitCourtSitting()}>
          <Text style={styles.primaryButtonText}>
            {courtSittingSubmitting ? 'Saving…' : 'Add court sitting'}
          </Text>
        </Pressable>
      </View>

      <DiscreetExpandModal
        visible={discreet.stepUpVisible}
        loading={discreet.stepUpLoading}
        errorMessage={discreet.stepUpError}
        onClose={discreet.closeStepUp}
        onSubmit={code => void discreet.onStepUpSubmit(code)}
      />
    </ScrollView>
  );
}

function badgeStyle(noteType: string | null | undefined) {
  switch (noteType) {
    case 'Visit':
      return styles.badgeVisit;
    case 'Court':
      return styles.badgeCourt;
    case 'Intervention':
      return styles.badgeIntervention;
    default:
      return styles.badgeGeneral;
  }
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#f8fafc',
  },
  content: {
    padding: 16,
    paddingBottom: 32,
  },
  title: {
    fontSize: 22,
    fontWeight: '600',
  },
  subtitle: {
    color: '#475569',
    marginBottom: 12,
  },
  headerCard: {
    backgroundColor: '#fff',
    borderWidth: 1,
    borderColor: '#E4E7EC',
    borderRadius: 12,
    padding: 16,
    marginBottom: 12,
  },
  error: {
    color: '#b42318',
    marginBottom: 8,
  },
  retryButton: {
    alignSelf: 'flex-start',
    marginBottom: 12,
  },
  retryText: {
    color: '#175cd3',
    fontWeight: '600',
  },
  whisper: {
    marginTop: 12,
    backgroundColor: '#EFF8FF',
    borderLeftWidth: 3,
    borderLeftColor: '#175CD3',
    paddingVertical: 10,
    paddingHorizontal: 12,
  },
  whisperLine: {
    marginBottom: 4,
  },
  timelineLink: {
    marginTop: 6,
    color: '#175CD3',
    textDecorationLine: 'underline',
  },
  notesSection: {
    marginTop: 16,
    paddingTop: 12,
    borderTopWidth: 1,
    borderTopColor: '#E4E7EC',
  },
  sectionTitle: {
    fontSize: 18,
    fontWeight: '600',
    marginBottom: 8,
  },
  emptyState: {
    color: '#475569',
    marginBottom: 8,
  },
  noteItem: {
    paddingVertical: 10,
    borderBottomWidth: 1,
    borderBottomColor: '#E4E7EC',
  },
  interventionOverdue: {
    backgroundColor: '#FEF3F2',
    borderLeftWidth: 3,
    borderLeftColor: '#B42318',
    paddingLeft: 8,
  },
  courtSittingPastDue: {
    backgroundColor: '#FFFAEB',
    borderLeftWidth: 4,
    borderLeftColor: '#B54708',
    paddingLeft: 8,
  },
  noteHeader: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    alignItems: 'center',
    gap: 8,
    marginBottom: 4,
  },
  badge: {
    paddingHorizontal: 8,
    paddingVertical: 2,
    borderRadius: 4,
    overflow: 'hidden',
    color: '#fff',
    fontSize: 11,
    fontWeight: '700',
    textTransform: 'uppercase',
  },
  badgeVisit: {
    backgroundColor: '#0D6E6E',
  },
  badgeCourt: {
    backgroundColor: '#175CD3',
  },
  badgeIntervention: {
    backgroundColor: '#B54708',
  },
  badgeGeneral: {
    backgroundColor: '#667085',
  },
  badgeOverdue: {
    backgroundColor: '#B42318',
  },
  noteMeta: {
    color: '#475569',
    fontSize: 12,
  },
  noteBody: {
    marginBottom: 4,
  },
  actionRequired: {
    color: '#b42318',
    fontWeight: '600',
    fontSize: 12,
    marginBottom: 4,
  },
  attachmentChip: {
    alignSelf: 'flex-start',
    borderWidth: 1,
    borderColor: '#CBD5E1',
    borderRadius: 4,
    paddingHorizontal: 8,
    paddingVertical: 4,
    marginTop: 4,
  },
  attachmentText: {
    color: '#175CD3',
    fontSize: 12,
  },
  formTitle: {
    fontSize: 16,
    fontWeight: '600',
    marginTop: 12,
    marginBottom: 8,
  },
  typeRow: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 8,
    marginBottom: 8,
  },
  typeChip: {
    borderWidth: 1,
    borderColor: '#CBD5E1',
    borderRadius: 4,
    paddingHorizontal: 8,
    paddingVertical: 4,
  },
  typeChipSelected: {
    backgroundColor: '#0D6E6E',
    borderColor: '#0D6E6E',
  },
  typeChipText: {
    color: '#334155',
    fontSize: 12,
  },
  typeChipTextSelected: {
    color: '#fff',
    fontSize: 12,
    fontWeight: '600',
  },
  textInput: {
    borderWidth: 1,
    borderColor: '#CBD5E1',
    borderRadius: 8,
    minHeight: 96,
    padding: 10,
    marginBottom: 8,
    backgroundColor: '#fff',
    textAlignVertical: 'top',
  },
  checkboxRow: {
    marginBottom: 8,
  },
  dateButton: {
    borderWidth: 1,
    borderColor: '#CBD5E1',
    borderRadius: 8,
    padding: 10,
    marginBottom: 8,
  },
  dateButtonText: {
    color: '#334155',
  },
  datePreview: {
    marginBottom: 8,
    color: '#475569',
  },
  secondaryButton: {
    borderWidth: 1,
    borderColor: '#CBD5E1',
    borderRadius: 8,
    padding: 10,
    marginBottom: 8,
  },
  secondaryButtonText: {
    color: '#175CD3',
  },
  primaryButton: {
    backgroundColor: '#0D6E6E',
    borderRadius: 8,
    padding: 12,
    alignItems: 'center',
  },
  primaryButtonDisabled: {
    opacity: 0.6,
  },
  primaryButtonText: {
    color: '#fff',
    fontWeight: '600',
  },
});
