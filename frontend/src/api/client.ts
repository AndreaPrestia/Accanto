import axios from 'axios';

export const api = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL || '/api'
});

api.interceptors.request.use((cfg) => {
  const token = localStorage.getItem('accanto.token');
  if (token) {
    cfg.headers = cfg.headers ?? {};
    cfg.headers['Authorization'] = `Bearer ${token}`;
  }
  return cfg;
});

api.interceptors.response.use(
  (r) => r,
  (err) => {
    if (err?.response?.status === 401) {
      localStorage.removeItem('accanto.token');
      localStorage.removeItem('accanto.user');
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
