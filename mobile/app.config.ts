import { ConfigContext, ExpoConfig } from 'expo/config';

export default ({ config }: ConfigContext): ExpoConfig => ({
  ...config,
  name: 'Accanto',
  slug: 'accanto',
  scheme: 'accanto',
  version: '0.1.0',
  orientation: 'portrait',
  icon: './assets/icon.png',
  userInterfaceStyle: 'automatic',
  newArchEnabled: true,
  splash: {
    image: './assets/splash.png',
    resizeMode: 'contain',
    backgroundColor: '#f8fafc'
  },
  assetBundlePatterns: ['**/*'],
  ios: {
    supportsTablet: true,
    bundleIdentifier: 'app.accanto.mobile',
    associatedDomains: ['applinks:accanto.app'],
    infoPlist: {
      NSFaceIDUsageDescription:
        'Usa Face ID per sbloccare Accanto rapidamente senza inserire la password.'
    }
  },
  android: {
    package: 'app.accanto.mobile',
    adaptiveIcon: {
      foregroundImage: './assets/adaptive-icon.png',
      backgroundColor: '#f8fafc'
    },
    intentFilters: [
      {
        action: 'VIEW',
        autoVerify: true,
        data: [
          { scheme: 'https', host: 'accanto.app', pathPrefix: '/invite' }
        ],
        category: ['BROWSABLE', 'DEFAULT']
      }
    ]
  },
  web: {
    favicon: './assets/favicon.png'
  },
  plugins: [
    [
      'expo-notifications',
      {
        icon: './assets/notification-icon.png',
        color: '#0f172a'
      }
    ],
    'expo-secure-store',
    'expo-local-authentication',
    'expo-localization',
    'expo-font'
  ],
  extra: {
    eas: {
      projectId: process.env.EAS_PROJECT_ID ?? ''
    },
    apiBaseUrl: process.env.EXPO_PUBLIC_API_BASE_URL ?? 'https://api.accanto.app'
  },
  experiments: {
    typedRoutes: false
  }
});
