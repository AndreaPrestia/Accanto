import { createContext, useContext, useEffect, useState, ReactNode } from 'react';
import i18n, { SUPPORTED_LANGUAGES } from '../i18n';
import { api } from '../api/client';
import { AuthResponse, LoginRequest, RegisterRequest, User } from '../types';

interface AuthCtx {
  user: User | null;
  loading: boolean;
  login: (req: LoginRequest) => Promise<void>;
  register: (req: RegisterRequest) => Promise<void>;
  logout: () => void;
  setLanguage: (lang: string) => void;
}

const Ctx = createContext<AuthCtx | undefined>(undefined);

const TOKEN_KEY = 'accanto.token';
const REFRESH_KEY = 'accanto.refreshToken';
const USER_KEY = 'accanto.user';

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const raw = localStorage.getItem(USER_KEY);
    if (raw) {
      try {
        const u: User = JSON.parse(raw);
        setUser(u);
        if (u.language && (SUPPORTED_LANGUAGES as readonly string[]).includes(u.language)) {
          i18n.changeLanguage(u.language);
        }
      } catch { /* ignore */ }
    }
    setLoading(false);
  }, []);

  const persist = (res: AuthResponse) => {
    localStorage.setItem(TOKEN_KEY, res.accessToken);
    if (res.refreshToken) {
      localStorage.setItem(REFRESH_KEY, res.refreshToken);
    }
    localStorage.setItem(USER_KEY, JSON.stringify(res.user));
    setUser(res.user);
    if (res.user.language && (SUPPORTED_LANGUAGES as readonly string[]).includes(res.user.language)) {
      i18n.changeLanguage(res.user.language);
    }
  };

  const login = async (req: LoginRequest) => {
    const { data } = await api.post<AuthResponse>('/auth/login', req);
    persist(data);
  };

  const register = async (req: RegisterRequest) => {
    const { data } = await api.post<AuthResponse>('/auth/register', req);
    persist(data);
  };

  const logout = () => {
    const refreshToken = localStorage.getItem(REFRESH_KEY);
    if (refreshToken) {
      // Fire-and-forget: revoca lato server, ma non bloccare l'UX se la rete è giù.
      api.post('/auth/logout', { refreshToken }).catch(() => { /* ignore */ });
    }
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(REFRESH_KEY);
    localStorage.removeItem(USER_KEY);
    setUser(null);
  };

  const setLanguage = (lang: string) => {
    if (!(SUPPORTED_LANGUAGES as readonly string[]).includes(lang)) return;
    i18n.changeLanguage(lang);
    setUser(prev => {
      if (!prev) return prev;
      const next = { ...prev, language: lang };
      localStorage.setItem(USER_KEY, JSON.stringify(next));
      return next;
    });
  };

  return <Ctx.Provider value={{ user, loading, login, register, logout, setLanguage }}>{children}</Ctx.Provider>;
}

export function useAuth() {
  const v = useContext(Ctx);
  if (!v) throw new Error('useAuth fuori da AuthProvider');
  return v;
}
