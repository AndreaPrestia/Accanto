import { ConfigContext, ExpoConfig } from 'expo/config';

/**
 * APP_VARIANT viene popolato da eas.json (build) o dall'ambiente locale.
 * Permette di costruire build "parallele" sullo stesso device fisico
 * (production + preview installabili insieme senza overwrite).
 *
 *   undefined / "production" → app.accanto.mobile          / "Accanto"
 *   "preview"                → app.accanto.mobile.preview  / "Accanto (Preview)"
 *   "development"            → app.accanto.mobile.dev      / "Accanto (Dev)"
 */
type Variant = 'production' | 'preview' | 'development';

function resolveVariant(): Variant {
  const v = process.env.APP_VARIANT?.toLowerCase();
  if (v === 'preview') return 'preview';
  if (v === 'development' || v === 'dev') return 'development';
  return 'production';
}

function variantSuffix(variant: Variant): string {
  switch (variant) {
    case 'preview':
      return '.preview';
    case 'development':
      return '.dev';
    default:
      return '';
  }
}

function variantLabel(variant: Variant): string {
  switch (variant) {
    case 'preview':
      return ' (Preview)';
    case 'development':
      return ' (Dev)';
    default:
      return '';
  }
}

export default ({ config }: ConfigContext): ExpoConfig => {
  const variant = resolveVariant();
  const suffix = variantSuffix(variant);
  const label = variantLabel(variant);
  const bundleId = `app.accanto.mobile${suffix}`;
  const scheme = variant === 'production' ? 'accanto' : `accanto${suffix}`;

  return {
    ...config,
    name: `Accanto${label}`,
    slug: 'accanto',
    scheme,
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
      bundleIdentifier: bundleId,
      // applinks: copre TUTTI i path di accanto.care (universal links).
      // Niente activitycontinuation: non usiamo Handoff.
      associatedDomains: ['applinks:accanto.care'],
      infoPlist: {
        NSFaceIDUsageDescription:
          'Usa Face ID per sbloccare Accanto rapidamente senza inserire la password.',
        // Stringa richiesta da App Store review nel caso DocumentPicker
        // ricada sul photo picker per allegare immagini.
        NSPhotoLibraryUsageDescription:
          'Accanto chiede l\u2019accesso alla libreria solo se decidi di allegare un documento dalla galleria.'
      }
    },
    android: {
      package: bundleId,
      adaptiveIcon: {
        foregroundImage: './assets/adaptive-icon.png',
        backgroundColor: '#f8fafc'
      },
      // App Links Android: autoVerify=true richiede che
      // https://accanto.care/.well-known/assetlinks.json sia pubblicato e
      // contenga il SHA-256 del certificato di firma per QUESTO bundle.
      // Usiamo un unico host: tutti i deep link (/invite/...,
      // /care-circles/..., /reset-password) atterrano in app senza
      // chooser quando la verifica passa.
      intentFilters: [
        {
          action: 'VIEW',
          autoVerify: true,
          data: [{ scheme: 'https', host: 'accanto.care' }],
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
      'expo-font',
      '@react-native-community/datetimepicker'
    ],
    extra: {
      eas: {
        projectId: process.env.EAS_PROJECT_ID ?? ''
      },
      apiBaseUrl:
        process.env.EXPO_PUBLIC_API_BASE_URL ?? 'https://api.accanto.care',
      webBaseUrl:
        process.env.EXPO_PUBLIC_WEB_BASE_URL ?? 'https://accanto.care',
      variant
    },
    experiments: {
      typedRoutes: false
    }
  };
};
