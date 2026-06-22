import { linking } from '../linking';

describe('linking config', () => {
  it('include prefisso universal link su accanto.care', () => {
    expect(linking.prefixes).toContain('https://accanto.care');
  });

  it('include il prefisso scheme custom via Linking.createURL', () => {
    // jest.setup.js fa stub: createURL('/') -> 'accanto.dev:///'
    expect(linking.prefixes).toEqual(
      expect.arrayContaining([expect.stringMatching(/^accanto/)])
    );
  });

  it('mappa invite/:token sia su Auth che su App', () => {
    const screens = linking.config?.screens as any;
    expect(screens?.Auth?.screens?.InviteAccept).toBe('invite/:token');
    expect(screens?.App?.screens?.InviteAccept).toBe('invite/:token');
  });

  it('mappa reset-password su Auth', () => {
    const screens = linking.config?.screens as any;
    expect(screens?.Auth?.screens?.ResetPassword).toBe('reset-password');
  });

  it('mappa care-circles/:circleId sul Circle nested navigator', () => {
    const screens = linking.config?.screens as any;
    expect(screens?.App?.screens?.Circle?.path).toBe('care-circles/:circleId');
    expect(
      screens?.App?.screens?.Circle?.screens?.CircleTabs?.screens?.Timeline
    ).toBe('timeline');
  });

  it('mappa schermi pubblici support / self-care a livello Auth', () => {
    const screens = linking.config?.screens as any;
    expect(screens?.Auth?.screens?.Support).toBe('support');
    expect(screens?.Auth?.screens?.SelfCare).toBe('self-care');
  });
});
