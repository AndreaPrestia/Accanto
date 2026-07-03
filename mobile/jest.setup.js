/* global jest */
// Setup globale per Jest in ambiente Expo SDK 53.
// Mocha-style: niente top-level await, niente import dinamici — usare jest.mock.

// expo-constants: di default fornisce un valore minimo.
jest.mock('expo-constants', () => ({
  __esModule: true,
  default: {
    expoConfig: {
      extra: {
        apiBaseUrl: 'https://api.test.accanto.care',
        webBaseUrl: 'https://test.accanto.care',
        variant: 'development',
        eas: { projectId: 'test-project-id' }
      }
    },
    easConfig: { projectId: 'test-project-id' }
  }
}));

// expo-secure-store: in-memory mock per evitare dipendenza nativa.
jest.mock('expo-secure-store', () => {
  const store = new Map();
  return {
    __esModule: true,
    WHEN_UNLOCKED: 'WHEN_UNLOCKED',
    getItemAsync: jest.fn((k) => Promise.resolve(store.get(k) ?? null)),
    setItemAsync: jest.fn((k, v) => {
      store.set(k, v);
      return Promise.resolve();
    }),
    deleteItemAsync: jest.fn((k) => {
      store.delete(k);
      return Promise.resolve();
    })
  };
});

// AsyncStorage: mock ufficiale fornito dal pacchetto.
jest.mock('@react-native-async-storage/async-storage', () =>
  require('@react-native-async-storage/async-storage/jest/async-storage-mock')
);

// expo-linking: createURL diventa deterministico nei test.
jest.mock('expo-linking', () => ({
  __esModule: true,
  createURL: (path) => `accanto.dev://${path.replace(/^\//, '')}`,
  parse: jest.fn(),
  openURL: jest.fn()
}));

// expo-notifications + expo-device: surface ridotta, ogni test poi
// può fare override con jest.spyOn / jest.mocked.
jest.mock('expo-notifications', () => ({
  __esModule: true,
  AndroidImportance: { DEFAULT: 3 },
  getPermissionsAsync: jest.fn(() => Promise.resolve({ status: 'granted' })),
  requestPermissionsAsync: jest.fn(() => Promise.resolve({ status: 'granted' })),
  getExpoPushTokenAsync: jest.fn(() =>
    Promise.resolve({ data: 'ExponentPushToken[test]' })
  ),
  setNotificationChannelAsync: jest.fn(() => Promise.resolve()),
  setNotificationHandler: jest.fn(),
  addNotificationReceivedListener: jest.fn(() => ({ remove: jest.fn() })),
  addNotificationResponseReceivedListener: jest.fn(() => ({ remove: jest.fn() }))
}));

jest.mock('expo-device', () => ({
  __esModule: true,
  isDevice: true,
  deviceName: 'Test Device',
  brand: 'TestBrand',
  modelName: 'TestModel'
}));

// react-i18next: hook minimale per i componenti che usano useTranslation.
jest.mock('react-i18next', () => ({
  __esModule: true,
  useTranslation: () => ({
    t: (k) => k,
    i18n: { changeLanguage: jest.fn(), language: 'it' }
  }),
  initReactI18next: { type: '3rdParty', init: jest.fn() },
  Trans: ({ children }) => children
}));
