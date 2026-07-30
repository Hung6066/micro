export type HisHopeMfaMethod = "passkey" | "mobileApproval" | "totp";

export interface HisHopeAdaptiveMfaState {
  preferredMethod: HisHopeMfaMethod | null;
  availableMethods: HisHopeMfaMethod[];
  unfamiliarDevice: boolean;
  alternateMethodsOpen: boolean;
}

export interface CreateHisHopeAdaptiveMfaStateOptions {
  availableMethods: readonly HisHopeMfaMethod[];
  unfamiliarDevice?: boolean;
  preferredMethod?: HisHopeMfaMethod | null;
  alternateMethodsOpen?: boolean;
}

const HIS_HOPE_MFA_METHOD_ORDER: readonly HisHopeMfaMethod[] = [
  "passkey",
  "mobileApproval",
  "totp",
];

export function createHisHopeAdaptiveMfaState(
  options: CreateHisHopeAdaptiveMfaStateOptions,
): HisHopeAdaptiveMfaState {
  const availableMethods = normalizeHisHopeMfaMethods(options.availableMethods);
  const unfamiliarDevice = options.unfamiliarDevice ?? false;
  const preferredMethod = resolveHisHopeAdaptiveMfaPreferredMethod({
    availableMethods,
    unfamiliarDevice,
    preferredMethod: options.preferredMethod ?? null,
  });

  return {
    preferredMethod,
    availableMethods,
    unfamiliarDevice,
    alternateMethodsOpen: options.alternateMethodsOpen ?? false,
  };
}

export function setHisHopeAdaptiveMfaAlternateMethodsOpen(
  state: HisHopeAdaptiveMfaState,
  open: boolean,
): HisHopeAdaptiveMfaState {
  return {
    ...state,
    alternateMethodsOpen: open,
  };
}

export function getHisHopeAdaptiveMfaAlternateMethods(
  state: HisHopeAdaptiveMfaState,
): HisHopeMfaMethod[] {
  if (state.preferredMethod === null) {
    return [...state.availableMethods];
  }

  return state.availableMethods.filter(
    (method) => method !== state.preferredMethod,
  );
}

function normalizeHisHopeMfaMethods(
  methods: readonly HisHopeMfaMethod[],
): HisHopeMfaMethod[] {
  const available = new Set(methods);
  return HIS_HOPE_MFA_METHOD_ORDER.filter((method) => available.has(method));
}

function resolveHisHopeAdaptiveMfaPreferredMethod({
  availableMethods,
  unfamiliarDevice,
  preferredMethod,
}: {
  availableMethods: readonly HisHopeMfaMethod[];
  unfamiliarDevice: boolean;
  preferredMethod: HisHopeMfaMethod | null;
}): HisHopeMfaMethod | null {
  if (preferredMethod && availableMethods.includes(preferredMethod)) {
    return preferredMethod;
  }

  if (unfamiliarDevice && availableMethods.includes("mobileApproval")) {
    return "mobileApproval";
  }

  if (availableMethods.includes("passkey")) {
    return "passkey";
  }

  if (availableMethods.includes("mobileApproval")) {
    return "mobileApproval";
  }

  if (availableMethods.includes("totp")) {
    return "totp";
  }

  return null;
}
