import axios, { AxiosError, InternalAxiosRequestConfig } from 'axios';

// Chiavi di storage DEDICATE all'admin (distinte da 'accanto.*' della PWA
// pubblica) per non mescolare i due token sullo stesso browser.
const TOKEN_KEY = 'accanto.admin.token';
const REFRESH_KEY = 'accanto.admin.refreshToken';
const USER_KEY = 'accanto.admin.user';

export const adminApi = axios.create({
  baseURL: import.meta.env.VITE_ADMIN_API_BASE_URL || '/admin-api'
});

export function getAccessToken(): string | null {
  return localStorage.getItem(TOKEN_KEY);
}
export function getRefreshToken(): string | null {
  return localStorage.getItem(REFRESH_KEY);
}
export function persistSession(accessToken: string, refreshToken: string, userJson: string) {
  localStorage.setItem(TOKEN_KEY, accessToken);
  localStorage.setItem(REFRESH_KEY, refreshToken);
  localStorage.setItem(USER_KEY, userJson);
}
export function loadStoredUser(): string | null {
  return localStorage.getItem(USER_KEY);
}
export function clearSession() {
  localStorage.removeItem(TOKEN_KEY);
  localStorage.removeItem(REFRESH_KEY);
  localStorage.removeItem(USER_KEY);
}

adminApi.interceptors.request.use((cfg) => {
  const token = getAccessToken();
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
  const refreshToken = getRefreshToken();
  if (!refreshToken) return null;
  try {
    const baseURL = adminApi.defaults.baseURL ?? '/admin-api';
    const { data } = await axios.post(
      `${baseURL}/api/admin/auth/refresh`,
      { refreshToken },
      { headers: { 'Content-Type': 'application/json' } }
    );
    if (data?.accessToken && data?.refreshToken) {
      localStorage.setItem(TOKEN_KEY, data.accessToken);
      localStorage.setItem(REFRESH_KEY, data.refreshToken);
      if (data.adminUser) localStorage.setItem(USER_KEY, JSON.stringify(data.adminUser));
      return data.accessToken as string;
    }
    return null;
  } catch {
    return null;
  }
}

adminApi.interceptors.response.use(
  (r) => r,
  async (err: AxiosError) => {
    const status = err?.response?.status;
    const original = err.config as RetriableConfig;
    const url = original?.url ?? '';
    const isAuthEndpoint =
      url.includes('/auth/login') || url.includes('/auth/refresh') || url.includes('/auth/logout');

    if (status === 401 && original && !original._retry && !isAuthEndpoint) {
      original._retry = true;
      refreshPromise = refreshPromise ?? refreshAccessToken();
      const newToken = await refreshPromise;
      refreshPromise = null;
      if (newToken) {
        original.headers = original.headers ?? {};
        (original.headers as Record<string, string>)['Authorization'] = `Bearer ${newToken}`;
        return adminApi.request(original);
      }
      clearSession();
      if (typeof window !== 'undefined' && !window.location.pathname.startsWith('/login')) {
        window.location.href = '/login';
      }
    }
    return Promise.reject(err);
  }
);
