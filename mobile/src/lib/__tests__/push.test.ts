import * as Notifications from 'expo-notifications';
import * as Device from 'expo-device';
import { api } from '../../api/client';
import {
  registerForPushNotificationsAsync,
  unregisterPushTokenAsync,
  getCurrentPushToken,
  configureForegroundHandler
} from '../push';

jest.mock('../../api/client', () => ({
  api: {
    post: jest.fn(() => Promise.resolve({ data: {} })),
    delete: jest.fn(() => Promise.resolve({ data: {} }))
  }
}));

describe('push.ts', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    // reset cache interna del modulo
    (Device as any).isDevice = true;
  });

  describe('registerForPushNotificationsAsync', () => {
    it('ritorna null su simulatore (Device.isDevice = false)', async () => {
      (Device as any).isDevice = false;
      const tok = await registerForPushNotificationsAsync();
      expect(tok).toBeNull();
      expect(api.post).not.toHaveBeenCalled();
    });

    it('ritorna null se permesso negato', async () => {
      (Notifications.getPermissionsAsync as jest.Mock).mockResolvedValueOnce({
        status: 'undetermined'
      });
      (Notifications.requestPermissionsAsync as jest.Mock).mockResolvedValueOnce(
        { status: 'denied' }
      );
      const tok = await registerForPushNotificationsAsync();
      expect(tok).toBeNull();
      expect(api.post).not.toHaveBeenCalled();
    });

    it('registra il token sul backend e lo cache-a', async () => {
      const tok = await registerForPushNotificationsAsync();
      expect(tok).toBe('ExponentPushToken[test]');
      expect(getCurrentPushToken()).toBe('ExponentPushToken[test]');
      expect(api.post).toHaveBeenCalledWith(
        '/account/push-devices',
        expect.objectContaining({
          token: 'ExponentPushToken[test]',
          platform: expect.any(String)
        })
      );
    });

    it('è best-effort: non solleva se il backend fallisce', async () => {
      (api.post as jest.Mock).mockRejectedValueOnce(new Error('500'));
      const tok = await registerForPushNotificationsAsync();
      // L'errore è catturato e loggato; il return è null.
      expect(tok).toBeNull();
    });
  });

  describe('unregisterPushTokenAsync', () => {
    it('chiama DELETE con il token in cache', async () => {
      await registerForPushNotificationsAsync();
      await unregisterPushTokenAsync();
      expect(api.delete).toHaveBeenCalledWith(
        '/account/push-devices',
        expect.objectContaining({
          data: { token: 'ExponentPushToken[test]' }
        })
      );
      expect(getCurrentPushToken()).toBeNull();
    });

    it('no-op se nessun token disponibile', async () => {
      await unregisterPushTokenAsync(null);
      expect(api.delete).not.toHaveBeenCalled();
    });

    it('best-effort: non solleva se il backend fallisce', async () => {
      (api.delete as jest.Mock).mockRejectedValueOnce(new Error('500'));
      await expect(
        unregisterPushTokenAsync('ExponentPushToken[old]')
      ).resolves.toBeUndefined();
    });
  });

  describe('configureForegroundHandler', () => {
    it('imposta lo handler globale di expo-notifications', () => {
      configureForegroundHandler();
      expect(Notifications.setNotificationHandler).toHaveBeenCalledTimes(1);
    });
  });
});
