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

  it('mappa invite/:token sia su Auth che su App > Main', () => {
    const screens = linking.config?.screens as any;
    expect(screens?.Auth?.screens?.InviteAccept).toBe('invite/:token');
    expect(screens?.App?.screens?.Main?.screens?.InviteAccept).toBe('invite/:token');
  });

  it('mappa reset-password su Auth', () => {
    const screens = linking.config?.screens as any;
    expect(screens?.Auth?.screens?.ResetPassword).toBe('reset-password');
  });

  it('mappa care-circles/:circleId sul Circle nested navigator dentro Main', () => {
    const screens = linking.config?.screens as any;
    const main = screens?.App?.screens?.Main?.screens;
    expect(main?.Circle?.path).toBe('care-circles/:circleId');
    expect(main?.Circle?.screens?.CircleTabs?.screens?.Timeline).toBe('timeline');
  });

  it('espone Account / AiHistory / Support / SelfCare come voci del drawer', () => {
    const screens = linking.config?.screens as any;
    const app = screens?.App?.screens;
    expect(app?.Account).toBe('account');
    expect(app?.AiHistory).toBe('ai/history');
    expect(app?.Support).toBe('support');
    expect(app?.SelfCare).toBe('self-care');
  });

  it('mappa schermi pubblici support / self-care a livello Auth', () => {
    const screens = linking.config?.screens as any;
    expect(screens?.Auth?.screens?.Support).toBe('support');
    expect(screens?.Auth?.screens?.SelfCare).toBe('self-care');
  });
});
