import axios, { AxiosError, AxiosRequestConfig, InternalAxiosRequestConfig } from 'axios';

const TOKEN_KEY = 'accanto.token';
const REFRESH_KEY = 'accanto.refreshToken';
const USER_KEY = 'accanto.user';

export const api = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL || '/api'
});

api.interceptors.request.use((cfg) => {
  const token = localStorage.getItem(TOKEN_KEY);
  if (token) {
    cfg.headers = cfg.headers ?? {};
    cfg.headers['Authorization'] = `Bearer ${token}`;
  }
  return cfg;
});

interface RetriableConfig extends InternalAxiosRequestConfig {
  _retry?: boolean;
}

let refreshPromise: Promise<string | null> | null = null;

async function refreshAccessToken(): Promise<string | null> {
  const refreshToken = localStorage.getItem(REFRESH_KEY);
  if (!refreshToken) return null;
  try {
    const baseURL = api.defaults.baseURL ?? '/api';
    const { data } = await axios.post(
      `${baseURL}/auth/refresh`,
      { refreshToken },
      { headers: { 'Content-Type': 'application/json' } }
    );
    if (data?.accessToken && data?.refreshToken) {
      localStorage.setItem(TOKEN_KEY, data.accessToken);
      localStorage.setItem(REFRESH_KEY, data.refreshToken);
      if (data.user) localStorage.setItem(USER_KEY, JSON.stringify(data.user));
      return data.accessToken as string;
    }
    return null;
  } catch {
    return null;
  }
}

function clearSession() {
  localStorage.removeItem(TOKEN_KEY);
  localStorage.removeItem(REFRESH_KEY);
  localStorage.removeItem(USER_KEY);
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
          (original.headers as Record<string, string>)['Authorization'] = `Bearer ${newToken}`;
          return api.request(original as AxiosRequestConfig);
        }
      } catch {
        refreshPromise = null;
      }
      clearSession();
      if (!location.pathname.startsWith('/login')) {
        location.href = '/login';
      }
    } else if (status === 401) {
      clearSession();
      if (!location.pathname.startsWith('/login')) {
        location.href = '/login';
      }
    }

    return Promise.reject(err);
  }
);

export type ApiError = {
  title?: string;
  detail?: string;
  errors?: Record<string, string[]>;
};

export function extractError(err: any): string {
  const data: ApiError | undefined = err?.response?.data;
  if (data?.errors) {
    return Object.values(data.errors).flat().join(' • ');
  }
  return data?.title || data?.detail || err?.message || 'Errore imprevisto';
}
