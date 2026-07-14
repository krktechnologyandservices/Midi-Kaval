import React, {useCallback, useEffect, useMemo, useState} from 'react';
import {
  ActivityIndicator,
  KeyboardAvoidingView,
  PermissionsAndroid,
  Platform,
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  View,
} from 'react-native';
import DateTimePicker from '@react-native-community/datetimepicker';
import DocumentPicker, {types} from 'react-native-document-picker';
import {launchCamera} from 'react-native-image-picker';
import {RouteProp, useNavigation, useRoute} from '@react-navigation/native';
import {NativeStackNavigationProp} from '@react-navigation/native-stack';
import {AccessibleErrorRegion} from '../../components/AccessibleErrorRegion';
import {Icon} from '../../components/Icon';
import {SyncChip} from '../../components/SyncChip';
import {MoreStackParamList} from '../../navigation/types';
import {attachmentApiService} from '../../services/attachments/AttachmentApiService';
import {caseApiService} from '../../services/cases/CaseApiService';
import {CaseDto} from '../../services/cases/case.models';
import {
  ALLOWED_ATTACHMENT_CONTENT_TYPES,
  MAX_ATTACHMENT_BYTES,
} from '../../services/cases/case.models';
import {findTravelDraftByKey, readOfflineQueue} from '../../services/sync/offlineQueue';
import {resolveTravelSyncChip} from '../../services/sync/resolveTravelSyncChip';
import {useSyncOnForeground} from '../../services/sync/useSyncOnForeground';
import {QueuedMutation} from '../../services/sync/syncMutationTypes';
import {isDeviceOffline} from '../../services/sync/networkStatus';
import {openAndroidDatePicker} from '../../utils/androidDatePicker';
import {
  travelClaimApiService,
  TravelClaimApiError,
} from '../../services/travel/TravelClaimApiService';
import {
  RECEIPT_REQUIRED_MESSAGE,
  requiresReceipt,
  TRANSPORT_MODES,
  TransportMode,
  TravelClaimDto,
} from '../../services/travel/travel.models';

type Route = RouteProp<MoreStackParamList, 'TravelClaimForm'>;
type Navigation = NativeStackNavigationProp<MoreStackParamList, 'TravelClaimForm'>;

type PickedReceipt = {
  uri: string;
  name: string;
  type: string;
  size: number;
};

function toClaimDateIso(date: Date): string {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}

function statusStyle(status: string | null | undefined) {
  switch (status) {
    case 'Submitted':
      return styles.statusSubmitted;
    case 'Approved':
      return styles.statusApproved;
    case 'Returned':
      return styles.statusReturned;
    default:
      return styles.statusDraft;
  }
}

function formatDecidedAt(value: string | null | undefined): string {
  if (!value) {
    return '—';
  }

  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? '—' : date.toLocaleString();
}

function validateForm(input: {
  startLocation: string;
  destination: string;
  transportMode: TransportMode;
  amountText: string;
  autoNumber: string;
  selectedCaseIds: string[];
  notes: string;
}): string | null {
  if (!input.startLocation.trim()) {
    return 'startLocation is required.';
  }

  if (input.startLocation.trim().length > 256) {
    return 'startLocation must be at most 256 characters.';
  }

  if (!input.destination.trim()) {
    return 'destination is required.';
  }

  if (input.destination.trim().length > 256) {
    return 'destination must be at most 256 characters.';
  }

  if (!TRANSPORT_MODES.includes(input.transportMode)) {
    return 'transportMode is required.';
  }

  const amount = Number.parseFloat(input.amountText);
  if (!Number.isFinite(amount) || amount <= 0 || amount > 999999.99) {
    return 'amount must be greater than zero and at most 999999.99.';
  }

  if (input.transportMode === 'Auto' && !input.autoNumber.trim()) {
    return 'autoNumber is required when transportMode is Auto.';
  }

  if (input.autoNumber.trim().length > 32) {
    return 'autoNumber must be at most 32 characters.';
  }

  if (!input.selectedCaseIds.length) {
    return 'caseIds must contain at least one case id.';
  }

  if (new Set(input.selectedCaseIds).size !== input.selectedCaseIds.length) {
    return 'caseIds must not contain duplicate case ids.';
  }

  if (input.notes.trim().length > 2000) {
    return 'notes must be at most 2000 characters.';
  }

  return null;
}

export function TravelClaimFormScreen(): React.JSX.Element {
  const navigation = useNavigation<Navigation>();
  const route = useRoute<Route>();
  const {claimId, localDraftKey, mode = 'create'} = route.params;

  const readOnly = mode === 'view';
  const [claim, setClaim] = useState<TravelClaimDto | null>(null);
  const [offlineQueue, setOfflineQueue] = useState<QueuedMutation[]>([]);
  const [assignedCases, setAssignedCases] = useState<CaseDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const [claimDate, setClaimDate] = useState(new Date());
  const [showDatePicker, setShowDatePicker] = useState(false);
  const [startLocation, setStartLocation] = useState('');
  const [destination, setDestination] = useState('');
  const [transportMode, setTransportMode] = useState<TransportMode>('Bus');
  const [amountText, setAmountText] = useState('');
  const [autoNumber, setAutoNumber] = useState('');
  const [notes, setNotes] = useState('');
  const [selectedCaseIds, setSelectedCaseIds] = useState<string[]>([]);
  const [pickedReceipt, setPickedReceipt] = useState<PickedReceipt | null>(null);
  const [isLocalDraft, setIsLocalDraft] = useState(false);
  const [activeLocalDraftKey, setActiveLocalDraftKey] = useState<string | undefined>(
    localDraftKey,
  );

  const receiptRequired = requiresReceipt(transportMode);
  const hasConfirmedReceipt = (claim?.attachments?.length ?? 0) > 0 || !!pickedReceipt;

  const syncChip = useMemo(() => {
    if (!isLocalDraft || !activeLocalDraftKey) {
      return null;
    }

    return resolveTravelSyncChip(activeLocalDraftKey, offlineQueue);
  }, [activeLocalDraftKey, isLocalDraft, offlineQueue]);

  const load = useCallback(async (): Promise<void> => {
    setLoading(true);
    setErrorMessage(null);

    try {
      const [casesResult, queue] = await Promise.all([
        caseApiService.listAssignedCases(1, 100),
        readOfflineQueue(),
      ]);
      setAssignedCases(casesResult.items ?? []);
      setOfflineQueue(queue);

      if (localDraftKey) {
        const draft = await findTravelDraftByKey(localDraftKey);
        if (!draft) {
          setErrorMessage('Local draft not found on this device.');
          return;
        }

        setIsLocalDraft(true);
        setActiveLocalDraftKey(draft.localDraftKey);
        setClaimDate(new Date(draft.claimDate));
        setStartLocation(draft.startLocation);
        setDestination(draft.destination);
        setTransportMode(draft.transportMode as TransportMode);
        setAmountText(String(draft.amount));
        setAutoNumber(draft.autoNumber ?? '');
        setNotes(draft.notes ?? '');
        setSelectedCaseIds(draft.caseIds);
        if (draft.localReceiptUri && draft.receiptFileName && draft.receiptContentType) {
          setPickedReceipt({
            uri: draft.localReceiptUri,
            name: draft.receiptFileName,
            type: draft.receiptContentType,
            size: 1,
          });
        }
        return;
      }

      if (claimId) {
        const loaded = await travelClaimApiService.get(claimId);
        setClaim(loaded);
        setClaimDate(loaded.claimDate ? new Date(loaded.claimDate) : new Date());
        setStartLocation(loaded.startLocation ?? '');
        setDestination(loaded.destination ?? '');
        setTransportMode((loaded.transportMode as TransportMode) ?? 'Bus');
        setAmountText(loaded.amount != null ? String(loaded.amount) : '');
        setAutoNumber(loaded.autoNumber ?? '');
        setNotes(loaded.notes ?? '');
        setSelectedCaseIds(loaded.caseIds ?? []);
      }
    } catch (error) {
      setErrorMessage(travelClaimApiService.extractErrorMessage(error));
    } finally {
      setLoading(false);
    }
  }, [claimId, localDraftKey]);

  useEffect(() => {
    void load();
  }, [load]);

  useSyncOnForeground({
    enabled: isLocalDraft,
    onSynced: async result => {
      if (!activeLocalDraftKey) {
        await load();
        return;
      }

      const syncedClaim = result.appliedTravelClaimsByLocalDraftKey.get(
        activeLocalDraftKey,
      );
      if (syncedClaim?.id) {
        navigation.replace('TravelClaimForm', {
          claimId: syncedClaim.id,
          mode: 'edit',
        });
        return;
      }

      const draft = await findTravelDraftByKey(activeLocalDraftKey);
      if (draft) {
        await load();
        return;
      }

      await load();
    },
  });

  const onTransportModeChange = (next: TransportMode): void => {
    setTransportMode(next);
    if (next !== 'Auto') {
      setAutoNumber('');
    }
  };

  const toggleCase = (caseItemId: string | undefined): void => {
    if (!caseItemId || readOnly) {
      return;
    }

    setSelectedCaseIds(prev =>
      prev.includes(caseItemId)
        ? prev.filter(id => id !== caseItemId)
        : [...prev, caseItemId],
    );
  };

  const pickReceipt = async (): Promise<void> => {
    if (readOnly || isLocalDraft) {
      return;
    }

    try {
      const result = await DocumentPicker.pickSingle({
        type: [types.images, types.pdf],
        copyTo: 'cachesDirectory',
      });

      const mime = result.type ?? 'application/octet-stream';
      if (
        !ALLOWED_ATTACHMENT_CONTENT_TYPES.includes(
          mime as (typeof ALLOWED_ATTACHMENT_CONTENT_TYPES)[number],
        )
      ) {
        setErrorMessage('File type not allowed. Use JPEG, PNG, WebP, or PDF.');
        return;
      }

      const size = result.size;
      if (size == null || size <= 0) {
        setErrorMessage('Could not determine file size. Try another file.');
        return;
      }

      if (size > MAX_ATTACHMENT_BYTES) {
        setErrorMessage('File exceeds 10 MiB limit.');
        return;
      }

      setPickedReceipt({
        uri: result.fileCopyUri ?? result.uri,
        name: result.name ?? 'receipt',
        type: mime,
        size,
      });
      setErrorMessage(null);
    } catch (error) {
      if (!DocumentPicker.isCancel(error)) {
        setErrorMessage('Could not pick receipt.');
      }
    }
  };

  const captureReceiptPhoto = async (): Promise<void> => {
    if (readOnly || isLocalDraft) {
      return;
    }

    if (Platform.OS === 'android') {
      // react-native-image-picker checks the CAMERA permission but never requests it
      // (see its Utils.isCameraPermissionFulfilled) — without this explicit request the
      // OS permission popup never appears and launchCamera just fails silently with a
      // permission error on first use.
      const alreadyGranted = await PermissionsAndroid.check(
        PermissionsAndroid.PERMISSIONS.CAMERA,
      );
      if (!alreadyGranted) {
        const result = await PermissionsAndroid.request(
          PermissionsAndroid.PERMISSIONS.CAMERA,
        );
        if (result !== PermissionsAndroid.RESULTS.GRANTED) {
          setErrorMessage('Camera permission is required to take a photo. Check camera permission in device settings.');
          return;
        }
      }
    }

    let response;
    try {
      response = await launchCamera({
        mediaType: 'photo',
        saveToPhotos: false,
        quality: 0.8,
      });
    } catch {
      // launchCamera() can reject outright (not just resolve with errorCode) on some
      // devices/OS versions — without this catch, the promise rejection was unhandled
      // and the button appeared to silently do nothing.
      setErrorMessage('Could not open camera. Try again.');
      return;
    }

    if (response.didCancel) {
      return;
    }

    if (response.errorCode) {
      setErrorMessage(
        response.errorCode === 'camera_unavailable' || response.errorCode === 'permission'
          ? 'Camera is not available. Check camera permission in device settings.'
          : 'Could not capture photo.',
      );
      return;
    }

    const asset = response.assets?.[0];
    if (!asset?.uri) {
      setErrorMessage('Could not capture photo.');
      return;
    }

    const mime = asset.type ?? 'image/jpeg';
    if (
      !ALLOWED_ATTACHMENT_CONTENT_TYPES.includes(
        mime as (typeof ALLOWED_ATTACHMENT_CONTENT_TYPES)[number],
      )
    ) {
      setErrorMessage('Photo format not allowed. Use JPEG, PNG, or WebP.');
      return;
    }

    const size = asset.fileSize;
    if (size == null || size <= 0) {
      setErrorMessage('Could not determine photo size. Try again.');
      return;
    }

    if (size > MAX_ATTACHMENT_BYTES) {
      setErrorMessage('Photo exceeds 10 MiB limit.');
      return;
    }

    setPickedReceipt({
      uri: asset.uri,
      name: asset.fileName ?? `receipt-${Date.now()}.jpg`,
      type: mime,
      size,
    });
    setErrorMessage(null);
  };

  const uploadReceipt = async (targetClaimId: string): Promise<void> => {
    if (!pickedReceipt) {
      return;
    }

    await attachmentApiService.upload({
      resourceType: 'TravelClaim',
      resourceId: targetClaimId,
      fileUri: pickedReceipt.uri,
      fileName: pickedReceipt.name,
      contentType: pickedReceipt.type,
    });
  };

  const saveDraft = async (): Promise<void> => {
    const validationError = validateForm({
      startLocation,
      destination,
      transportMode,
      amountText,
      autoNumber,
      selectedCaseIds,
      notes,
    });

    if (validationError) {
      setErrorMessage(validationError);
      return;
    }

    setSaving(true);
    setErrorMessage(null);

    const payload = {
      claimDate: toClaimDateIso(claimDate),
      startLocation: startLocation.trim(),
      destination: destination.trim(),
      transportMode,
      amount: Number.parseFloat(amountText),
      autoNumber: transportMode === 'Auto' ? autoNumber.trim() : null,
      notes: notes.trim() || null,
      caseIds: selectedCaseIds,
    };

    try {
      if (mode === 'create' && !claimId && !isLocalDraft) {
        if (await isDeviceOffline()) {
          await travelClaimApiService.createOfflineDraft({
            ...payload,
            localReceiptUri: pickedReceipt?.uri,
            receiptFileName: pickedReceipt?.name,
            receiptContentType: pickedReceipt?.type,
          });
          navigation.navigate('TravelClaimsList');
          return;
        }

        const created = await travelClaimApiService.create(payload);
        if (pickedReceipt && created.id) {
          try {
            await uploadReceipt(created.id);
          } catch (uploadError) {
            // The claim record was already created on the server at this point — if the
            // receipt upload fails and this falls through to the outer catch below, the
            // screen stays in "create" mode with no claimId, so tapping Save again would
            // create a second, duplicate claim. Navigate to the real claim's edit mode
            // regardless, and surface the upload failure separately.
            navigation.replace('TravelClaimForm', {
              claimId: created.id,
              mode: 'edit',
            });
            setErrorMessage(travelClaimApiService.extractErrorMessage(uploadError));
            return;
          }
        }
        navigation.replace('TravelClaimForm', {
          claimId: created.id,
          mode: 'edit',
        });
        return;
      }

      if (isLocalDraft) {
        setErrorMessage('This draft is saved on this device — sync when online to edit on server.');
        return;
      }

      if (!claimId) {
        setErrorMessage('Claim id is required to save changes.');
        return;
      }

      const updated = await travelClaimApiService.update(claimId, payload);
      setClaim(updated);
    } catch (error) {
      if (
        error instanceof TravelClaimApiError &&
        error.kind === 'network' &&
        mode === 'create' &&
        !claimId
      ) {
        try {
          await travelClaimApiService.createOfflineDraft({
            ...payload,
            localReceiptUri: pickedReceipt?.uri,
            receiptFileName: pickedReceipt?.name,
            receiptContentType: pickedReceipt?.type,
          });
          navigation.navigate('TravelClaimsList');
          return;
        } catch (enqueueError) {
          setErrorMessage(travelClaimApiService.extractErrorMessage(enqueueError));
          return;
        }
      }

      setErrorMessage(travelClaimApiService.extractErrorMessage(error));
    } finally {
      setSaving(false);
    }
  };

  const submitClaim = async (): Promise<void> => {
    if (!claimId || readOnly || isLocalDraft) {
      return;
    }

    if (receiptRequired && !hasConfirmedReceipt) {
      setErrorMessage(RECEIPT_REQUIRED_MESSAGE);
      return;
    }

    setSubmitting(true);
    setErrorMessage(null);

    try {
      const fresh = await travelClaimApiService.get(claimId);
      if (fresh.status !== 'Draft') {
        setErrorMessage('Only draft claims can be submitted.');
        return;
      }

      if (pickedReceipt) {
        await uploadReceipt(claimId);
      }

      await travelClaimApiService.submit(claimId);
      navigation.navigate('TravelClaimsList');
    } catch (error) {
      const message = travelClaimApiService.extractErrorMessage(error);
      setErrorMessage(
        message.includes('Receipt image is required')
          ? RECEIPT_REQUIRED_MESSAGE
          : message,
      );
    } finally {
      setSubmitting(false);
    }
  };

  if (loading) {
    return (
      <View style={styles.centered}>
        <ActivityIndicator color="#0D6E6E" />
      </View>
    );
  }

  return (
    <KeyboardAvoidingView
      style={styles.screen}
      behavior={Platform.OS === 'ios' ? 'padding' : undefined}>
    <ScrollView style={styles.container} contentContainerStyle={styles.content}>
      <View style={styles.headerRow}>
        <Icon name="bus-clock" size={26} color="#0D6E6E" />
        <View style={styles.headerTextGroup}>
          <Text style={styles.title}>
            {readOnly ? 'Travel claim' : claimId ? 'Edit travel claim' : 'New travel claim'}
          </Text>
          <Text style={styles.subtitle}>
            {readOnly
              ? 'Read-only — submitted claims cannot be edited.'
              : 'Fields marked * are required.'}
          </Text>
        </View>
      </View>

      {syncChip ? (
        <View style={styles.syncRow}>
          <SyncChip chip={syncChip} />
        </View>
      ) : null}

      {readOnly && claim?.status ? (
        <View style={styles.statusRow}>
          <Text style={[styles.statusChip, statusStyle(claim.status)]}>{claim.status}</Text>
        </View>
      ) : null}

      {readOnly && (claim?.status === 'Approved' || claim?.status === 'Returned') ? (
        <View style={styles.decisionSection} accessibilityLabel="Director feedback">
          <Text style={styles.decisionHeading}>Director feedback</Text>
          {claim.decisionComment ? (
            <Text style={styles.decisionComment}>{claim.decisionComment}</Text>
          ) : claim.status === 'Approved' ? (
            <Text style={styles.decisionComment}>Your claim was approved.</Text>
          ) : null}
          <Text style={styles.decisionMeta}>Decided {formatDecidedAt(claim.decidedAtUtc)}</Text>
        </View>
      ) : null}

      <Text style={styles.label}>Claim date *</Text>
      {readOnly || isLocalDraft ? (
        <Text style={styles.readOnlyValue}>{claimDate.toLocaleDateString()}</Text>
      ) : (
        <Pressable
          style={styles.dateInput}
          onPress={() =>
            Platform.OS === 'android'
              ? openAndroidDatePicker({value: claimDate, mode: 'date', onChange: setClaimDate})
              : setShowDatePicker(true)
          }
          accessibilityRole="button"
          accessibilityLabel="Claim date">
          <Text style={styles.dateInputText}>{claimDate.toLocaleDateString()}</Text>
          <Icon name="calendar-blank-outline" size={20} color="#475467" />
        </Pressable>
      )}
      {Platform.OS !== 'android' && showDatePicker ? (
        <DateTimePicker
          value={claimDate}
          mode="date"
          onChange={(_, date) => {
            setShowDatePicker(false);
            if (date) {
              setClaimDate(date);
            }
          }}
        />
      ) : null}

      <Text style={styles.label}>Start location *</Text>
      <TextInput
        style={styles.input}
        value={startLocation}
        editable={!readOnly && !isLocalDraft}
        onChangeText={setStartLocation}
        placeholder="e.g., District office"
        placeholderTextColor="#98A2B3"
        accessibilityLabel="Start location"
      />

      <Text style={styles.label}>Destination *</Text>
      <TextInput
        style={styles.input}
        value={destination}
        editable={!readOnly && !isLocalDraft}
        onChangeText={setDestination}
        placeholder="e.g., Beneficiary's residence"
        placeholderTextColor="#98A2B3"
        accessibilityLabel="Destination"
      />

      <Text style={styles.label}>Transport mode *</Text>
      <View style={styles.modeRow}>
        {TRANSPORT_MODES.map(modeOption => (
          <Pressable
            key={modeOption}
            style={[
              styles.modeChip,
              transportMode === modeOption ? styles.modeChipActive : null,
            ]}
            disabled={readOnly || isLocalDraft}
            onPress={() => onTransportModeChange(modeOption)}
            accessibilityRole="button"
            accessibilityLabel={`Transport mode ${modeOption}`}>
            <Text
              style={
                transportMode === modeOption
                  ? styles.modeChipTextActive
                  : styles.modeChipText
              }>
              {modeOption}
            </Text>
          </Pressable>
        ))}
      </View>

      <Text style={styles.label}>Amount *</Text>
      <View style={styles.amountRow}>
        <Text style={styles.currencyPrefix}>₹</Text>
        <TextInput
          style={[styles.input, styles.amountInput]}
          value={amountText}
          editable={!readOnly && !isLocalDraft}
          keyboardType="decimal-pad"
          onChangeText={setAmountText}
          placeholder="0.00"
          placeholderTextColor="#98A2B3"
          accessibilityLabel="Amount"
        />
      </View>

      {transportMode === 'Auto' ? (
        <>
          <Text style={styles.label}>Auto number *</Text>
          <TextInput
            style={styles.input}
            value={autoNumber}
            editable={!readOnly && !isLocalDraft}
            onChangeText={setAutoNumber}
            placeholder="e.g., KA01AB1234"
            placeholderTextColor="#98A2B3"
            accessibilityLabel="Auto number"
          />
        </>
      ) : null}

      <Text style={styles.label}>Linked cases *</Text>
      <Text style={styles.helperText}>
        Select the case(s) this trip relates to — at least one is required.
      </Text>
      {assignedCases.length === 0 ? (
        <View style={styles.emptyState}>
          <Icon name="folder-alert-outline" size={22} color="#98A2B3" />
          <Text style={styles.emptyStateText}>
            No assigned cases found. You need at least one assigned case to submit a travel claim.
          </Text>
        </View>
      ) : null}
      {assignedCases.map(caseItem => {
        const selected = selectedCaseIds.includes(caseItem.id ?? '');
        return (
          <Pressable
            key={caseItem.id}
            style={[styles.caseRow, selected ? styles.caseRowSelected : null]}
            disabled={readOnly || isLocalDraft}
            onPress={() => toggleCase(caseItem.id)}
            accessibilityRole="checkbox"
            accessibilityState={{checked: selected}}>
            <Text style={styles.caseRowText}>
              {caseItem.crimeNumber ?? '—'} · {caseItem.stNumber ?? '—'}
            </Text>
          </Pressable>
        );
      })}

      <Text style={styles.label}>Notes</Text>
      <TextInput
        style={[styles.input, styles.notesInput]}
        value={notes}
        editable={!readOnly && !isLocalDraft}
        multiline
        onChangeText={setNotes}
        accessibilityLabel="Notes"
      />

      {!readOnly ? (
        <>
          <Text style={styles.label}>Receipt{receiptRequired ? ' *' : ' (optional)'}</Text>
          {claim?.attachments?.map(attachment => (
            <View key={attachment.id} style={styles.receiptRow}>
              <Icon name="file-check-outline" size={18} color="#027a48" />
              <Text style={styles.receiptName}>{attachment.originalFileName}</Text>
            </View>
          ))}
          {pickedReceipt ? (
            <View style={styles.receiptRow}>
              <Icon name="file-check-outline" size={18} color="#027a48" />
              <Text style={styles.receiptName}>{pickedReceipt.name}</Text>
            </View>
          ) : null}
          {!isLocalDraft ? (
            <View style={styles.receiptButtonRow}>
              <Pressable
                style={[styles.secondaryButton, styles.receiptButton]}
                onPress={() => void captureReceiptPhoto()}
                accessibilityRole="button"
                accessibilityLabel="Take photo of receipt">
                <Icon name="camera-outline" size={18} color="#0D6E6E" />
                <Text style={styles.secondaryButtonText}>Take photo</Text>
              </Pressable>
              <Pressable
                style={[styles.secondaryButton, styles.receiptButton]}
                onPress={() => void pickReceipt()}
                accessibilityRole="button"
                accessibilityLabel="Pick receipt">
                <Icon name="file-document-outline" size={18} color="#0D6E6E" />
                <Text style={styles.secondaryButtonText}>
                  {pickedReceipt || claim?.attachments?.length
                    ? 'Replace file'
                    : 'Choose file'}
                </Text>
              </Pressable>
            </View>
          ) : null}
        </>
      ) : null}
    </ScrollView>

    {!readOnly ? (
      <View style={styles.footer}>
        <AccessibleErrorRegion message={errorMessage} />

        {receiptRequired && !hasConfirmedReceipt && claimId && claim?.status === 'Draft' ? (
          <View style={styles.hintRow}>
            <Icon name="alert-circle-outline" size={16} color="#b54708" />
            <Text style={styles.hint}>{RECEIPT_REQUIRED_MESSAGE}</Text>
          </View>
        ) : null}

        <Pressable
          style={[styles.primaryButton, saving ? styles.buttonDisabled : null]}
          disabled={saving}
          onPress={() => void saveDraft()}
          accessibilityRole="button"
          accessibilityLabel="Save draft">
          {saving ? <ActivityIndicator color="#fff" /> : <Icon name="content-save-outline" size={18} color="#fff" />}
          <Text style={styles.primaryButtonText}>
            {saving ? 'Saving…' : isLocalDraft ? 'Saved on this device' : 'Save draft'}
          </Text>
        </Pressable>

        {claimId && !isLocalDraft && claim?.status === 'Draft' ? (
          <Pressable
            style={[
              styles.primaryButton,
              submitting || (receiptRequired && !hasConfirmedReceipt)
                ? styles.buttonDisabled
                : null,
            ]}
            disabled={submitting || (receiptRequired && !hasConfirmedReceipt)}
            onPress={() => void submitClaim()}
            accessibilityRole="button"
            accessibilityLabel="Submit claim">
            {submitting ? <ActivityIndicator color="#fff" /> : <Icon name="send-outline" size={18} color="#fff" />}
            <Text style={styles.primaryButtonText}>
              {submitting ? 'Submitting…' : 'Submit'}
            </Text>
          </Pressable>
        ) : null}
      </View>
    ) : null}
    </KeyboardAvoidingView>
  );
}

const styles = StyleSheet.create({
  screen: {
    flex: 1,
    backgroundColor: '#fff',
  },
  container: {
    flex: 1,
    backgroundColor: '#fff',
  },
  content: {
    padding: 16,
    gap: 8,
    paddingBottom: 32,
  },
  centered: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: '#fff',
  },
  headerRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 12,
    marginBottom: 16,
  },
  headerTextGroup: {
    flex: 1,
  },
  title: {
    fontSize: 20,
    fontWeight: '700',
    color: '#101828',
  },
  subtitle: {
    fontSize: 13,
    color: '#667085',
    marginTop: 2,
  },
  syncRow: {
    marginBottom: 8,
  },
  statusRow: {
    marginBottom: 8,
  },
  statusChip: {
    alignSelf: 'flex-start',
    fontSize: 12,
    paddingHorizontal: 8,
    paddingVertical: 2,
    borderRadius: 999,
    overflow: 'hidden',
  },
  statusDraft: {
    backgroundColor: '#f2f4f7',
    color: '#344054',
  },
  statusSubmitted: {
    backgroundColor: '#e0f2fe',
    color: '#026aa2',
  },
  statusApproved: {
    backgroundColor: '#ecfdf3',
    color: '#027a48',
  },
  statusReturned: {
    backgroundColor: '#fef3f2',
    color: '#b42318',
  },
  decisionSection: {
    borderWidth: 1,
    borderColor: '#d0d5dd',
    borderRadius: 8,
    padding: 12,
    marginBottom: 8,
    backgroundColor: '#f9fafb',
  },
  decisionHeading: {
    fontSize: 13,
    fontWeight: '600',
    color: '#344054',
    marginBottom: 6,
  },
  decisionComment: {
    fontSize: 14,
    color: '#101828',
    marginBottom: 6,
  },
  decisionMeta: {
    fontSize: 12,
    color: '#667085',
  },
  label: {
    fontSize: 13,
    fontWeight: '600',
    color: '#344054',
    marginTop: 8,
  },
  readOnlyValue: {
    fontSize: 15,
    color: '#101828',
    marginBottom: 4,
  },
  input: {
    borderWidth: 1,
    borderColor: '#d0d5dd',
    borderRadius: 8,
    paddingHorizontal: 12,
    paddingVertical: 10,
    fontSize: 15,
    color: '#101828',
  },
  notesInput: {
    minHeight: 80,
    textAlignVertical: 'top',
  },
  helperText: {
    fontSize: 12,
    color: '#667085',
    marginTop: -4,
    marginBottom: 4,
  },
  dateInput: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    borderWidth: 1,
    borderColor: '#d0d5dd',
    borderRadius: 8,
    paddingHorizontal: 12,
    paddingVertical: 10,
    minHeight: 44,
  },
  dateInputText: {
    fontSize: 15,
    color: '#101828',
  },
  amountRow: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  currencyPrefix: {
    fontSize: 15,
    fontWeight: '600',
    color: '#475467',
    paddingHorizontal: 12,
    borderWidth: 1,
    borderRightWidth: 0,
    borderColor: '#d0d5dd',
    borderTopLeftRadius: 8,
    borderBottomLeftRadius: 8,
    minHeight: 44,
    textAlignVertical: 'center',
    backgroundColor: '#f9fafb',
  },
  amountInput: {
    flex: 1,
    borderTopLeftRadius: 0,
    borderBottomLeftRadius: 0,
  },
  emptyState: {
    flexDirection: 'row',
    alignItems: 'flex-start',
    gap: 8,
    backgroundColor: '#f9fafb',
    borderWidth: 1,
    borderColor: '#eaecf0',
    borderRadius: 8,
    padding: 12,
    marginTop: 4,
  },
  emptyStateText: {
    flex: 1,
    fontSize: 13,
    color: '#667085',
  },
  modeRow: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 8,
  },
  modeChip: {
    borderWidth: 1,
    borderColor: '#d0d5dd',
    borderRadius: 999,
    paddingHorizontal: 12,
    paddingVertical: 6,
  },
  modeChipActive: {
    backgroundColor: '#0d6e6e',
    borderColor: '#0d6e6e',
  },
  modeChipText: {
    color: '#344054',
    fontSize: 13,
  },
  modeChipTextActive: {
    color: '#fff',
    fontSize: 13,
    fontWeight: '600',
  },
  caseRow: {
    borderWidth: 1,
    borderColor: '#d0d5dd',
    borderRadius: 8,
    padding: 10,
    marginTop: 4,
  },
  caseRowSelected: {
    borderColor: '#0d6e6e',
    backgroundColor: '#ecfdf3',
  },
  caseRowText: {
    fontSize: 14,
    color: '#101828',
  },
  receiptRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
    marginTop: 4,
  },
  receiptName: {
    fontSize: 14,
    color: '#475467',
  },
  footer: {
    borderTopWidth: 1,
    borderTopColor: '#eaecf0',
    backgroundColor: '#fff',
    paddingHorizontal: 16,
    paddingTop: 8,
    paddingBottom: 16,
  },
  hintRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
    marginTop: 4,
  },
  hint: {
    fontSize: 13,
    color: '#b54708',
  },
  primaryButton: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 8,
    marginTop: 12,
    backgroundColor: '#0d6e6e',
    borderRadius: 8,
    minHeight: 44,
  },
  secondaryButton: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 6,
    marginTop: 8,
    borderWidth: 1,
    borderColor: '#0d6e6e',
    borderRadius: 8,
    minHeight: 44,
  },
  receiptButtonRow: {
    flexDirection: 'row',
    gap: 8,
  },
  receiptButton: {
    flex: 1,
  },
  buttonDisabled: {
    opacity: 0.6,
  },
  primaryButtonText: {
    color: '#fff',
    fontWeight: '600',
    fontSize: 14,
  },
  secondaryButtonText: {
    color: '#0d6e6e',
    fontWeight: '600',
    fontSize: 14,
  },
});
