import Constants from 'expo-constants';

interface ExtraConfig {
  apiBaseUrl?: string;
  webBaseUrl?: string;
  eas?: { projectId?: string };
}

const extra = (Constants.expoConfig?.extra ?? {}) as ExtraConfig;

export const API_BASE_URL = extra.apiBaseUrl ?? 'https://api.accanto.app';
export const WEB_BASE_URL = extra.webBaseUrl ?? 'https://accanto.app';
