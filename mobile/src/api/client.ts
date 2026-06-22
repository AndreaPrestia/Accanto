import axios, {
  AxiosError,
  AxiosRequestConfig,
  InternalAxiosRequestConfig
} from 'axios';
import Constants from 'expo-constants';
import i18n from '../i18n';
import {
  clearRefreshToken,
  clearToken,
  getRefreshToken,
  getToken,
  setRefreshToken,
  setToken
} from '../storage/secureStorage';
import { clearStoredUser, setStoredUser } from '../storage/userStorage';
import { authEvents } from '../auth/events';
import type { AuthResponse } from '@accanto/shared/types';

// Risolve la baseURL da app.config.ts -> extra.apiBaseUrl, con override via
// EXPO_PUBLIC_API_BASE_URL al build / runtime.
const extra = (Constants.expoConfig?.extra ?? {}) as { apiBaseUrl?: string };
const baseURL = extra.apiBaseUrl ?? 'https://api.accanto.app';

export const api = axios.create({ baseURL });

api.interceptors.request.use(async (cfg) => {
  cfg.headers = cfg.headers ?? {};
  const token = await getToken();
  if (token) {
    cfg.headers['Authorization'] = `Bearer ${token}`;
  }
  cfg.headers['Accept-Language'] = i18n.language || 'it';
  return cfg;
});

interface RetriableConfig extends InternalAxiosRequestConfig {
  _retry?: boolean;
}

let refreshPromise: Promise<string | null> | null = null;

async function refreshAccessToken(): Promise<string | null> {
  const refreshToken = await getRefreshToken();
  if (!refreshToken) return null;
  try {
    const { data } = await axios.post<AuthResponse>(
      `${baseURL}/auth/refresh`,
      { refreshToken },
      { headers: { 'Content-Type': 'application/json' } }
    );
    if (data?.accessToken && data?.refreshToken) {
      await setToken(data.accessToken);
      await setRefreshToken(data.refreshToken);
      if (data.user) await setStoredUser(data.user);
      return data.accessToken;
    }
    return null;
  } catch {
    return null;
  }
}

async function clearSession(): Promise<void> {
  await clearToken();
  await clearRefreshToken();
  await clearStoredUser();
}

api.interceptors.response.use(
  (r) => r,
  async (err: AxiosError) => {
    const status = err?.response?.status;
    const original = err.config as RetriableConfig | undefined;
    const url = original?.url ?? '';
    const isAuthEndpoint =
      url.includes('/auth/login') ||
      url.includes('/auth/register') ||
      url.includes('/auth/refresh') ||
      url.includes('/auth/logout');

    if (status === 401 && original && !original._retry && !isAuthEndpoint) {
      original._retry = true;
      try {
        refreshPromise = refreshPromise ?? refreshAccessToken();
        const newToken = await refreshPromise;
        refreshPromise = null;
        if (newToken) {
          original.headers = original.headers ?? {};
          (original.headers as Record<string, string>)['Authorization'] =
            `Bearer ${newToken}`;
          return api.request(original as AxiosRequestConfig);
        }
      } catch {
        refreshPromise = null;
      }
      await clearSession();
      authEvents.emitSignedOut();
    } else if (status === 401 && !isAuthEndpoint) {
      await clearSession();
      authEvents.emitSignedOut();
    }

    return Promise.reject(err);
  }
);

export type ApiError = {
  title?: string;
  detail?: string;
  errors?: Record<string, string[]>;
};

export function extractError(err: unknown): string {
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const anyErr = err as any;
  const data: ApiError | undefined = anyErr?.response?.data;
  if (data?.errors) {
    return Object.values(data.errors).flat().join(' • ');
  }
  return data?.title || data?.detail || anyErr?.message || 'Errore imprevisto';
}
