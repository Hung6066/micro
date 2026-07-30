import {
  createHisHopeAdaptiveMfaState,
  setHisHopeAdaptiveMfaAlternatesOpen,
} from '@his-hope/frontend-foundation';

describe('His.Hope adaptive MFA state', () => {
  it('prefers passkey when passkey is available on a familiar device', () => {
    const state = createHisHopeAdaptiveMfaState({
      available: ['totp', 'passkey', 'mobileApproval'],
      unfamiliarDevice: false,
    });

    expect(state).toEqual({
      preferred: 'passkey',
      available: ['passkey', 'mobileApproval', 'totp'],
      unfamiliarDevice: false,
      alternateMethodsOpen: false,
    });
  });

  it('prefers mobile approval on an unfamiliar device when mobile approval is available', () => {
    const state = createHisHopeAdaptiveMfaState({
      available: ['passkey', 'mobileApproval', 'totp'],
      unfamiliarDevice: true,
    });

    expect(state.preferred).toBe('mobileApproval');
  });

  it('opens alternate methods only through an explicit deterministic transition', () => {
    const state = createHisHopeAdaptiveMfaState({
      available: ['passkey', 'totp'],
      unfamiliarDevice: false,
    });

    expect(setHisHopeAdaptiveMfaAlternatesOpen(state, true)).toEqual({
      ...state,
      alternateMethodsOpen: true,
    });
    expect(setHisHopeAdaptiveMfaAlternatesOpen(state, false)).toEqual({
      ...state,
      alternateMethodsOpen: false,
    });
  });

  it('keeps TOTP-only challenges valid', () => {
    const state = createHisHopeAdaptiveMfaState({
      available: ['totp'],
      unfamiliarDevice: true,
    });

    expect(state).toEqual({
      preferred: 'totp',
      available: ['totp'],
      unfamiliarDevice: true,
      alternateMethodsOpen: false,
    });
  });
});
