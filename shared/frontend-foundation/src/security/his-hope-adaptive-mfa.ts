export type HisHopeMfaMethod = 'passkey' | 'mobileApproval' | 'totp';

export interface HisHopeAdaptiveMfaState {
  readonly preferred: HisHopeMfaMethod;
  readonly available: readonly HisHopeMfaMethod[];
  readonly unfamiliarDevice: boolean;
  readonly alternateMethodsOpen: boolean;
}

export interface HisHopeAdaptiveMfaOptions {
  readonly available?: readonly HisHopeMfaMethod[];
  readonly unfamiliarDevice?: boolean;
  readonly alternateMethodsOpen?: boolean;
  readonly preferred?: HisHopeMfaMethod;
}

const HisHopeMfaMethodOrder: readonly HisHopeMfaMethod[] = ['passkey', 'mobileApproval', 'totp'];

export function createHisHopeAdaptiveMfaState(options: HisHopeAdaptiveMfaOptions = {}): HisHopeAdaptiveMfaState {
  const available = normalizeAvailableMfaMethods(options.available);
  return {
    preferred: choosePreferredMfaMethod(available, options.unfamiliarDevice === true, options.preferred),
    available,
    unfamiliarDevice: options.unfamiliarDevice === true,
    alternateMethodsOpen: options.alternateMethodsOpen === true,
  };
}

export function setHisHopeAdaptiveMfaAlternatesOpen(
  state: HisHopeAdaptiveMfaState,
  alternateMethodsOpen: boolean,
): HisHopeAdaptiveMfaState {
  return {
    ...state,
    alternateMethodsOpen,
  };
}

function normalizeAvailableMfaMethods(methods: readonly HisHopeMfaMethod[] | undefined): readonly HisHopeMfaMethod[] {
  const requested = methods?.length ? methods : HisHopeMfaMethodOrder;
  const available = HisHopeMfaMethodOrder.filter((method) => requested.includes(method));
  return available.length ? available : ['totp'];
}

function choosePreferredMfaMethod(
  available: readonly HisHopeMfaMethod[],
  unfamiliarDevice: boolean,
  preferred?: HisHopeMfaMethod,
): HisHopeMfaMethod {
  if (preferred && available.includes(preferred)) return preferred;
  if (unfamiliarDevice && available.includes('mobileApproval')) return 'mobileApproval';
  return available[0] ?? 'totp';
}
