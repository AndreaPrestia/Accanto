import { createContext, useContext, useEffect, useState, ReactNode } from 'react';
import { adminApi, persistSession, clearSession, loadStoredUser, getRefreshToken } from '../api/client';
import { AdminAuthResponse, AdminUser, LoginRequest } from '../types';

interface AuthCtx {
  user: AdminUser | null;
  loading: boolean;
  login: (req: LoginRequest) => Promise<void>;
  logout: () => void;
  hasRole: (...roles: string[]) => boolean;
  canMutate: boolean;
}

const Ctx = createContext<AuthCtx | undefined>(undefined);

const MUTATING_ROLES = ['Owner', 'Operator'];

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<AdminUser | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const raw = loadStoredUser();
    if (raw) {
      try { setUser(JSON.parse(raw)); } catch { /* ignore */ }
    }
    setLoading(false);
  }, []);

  const login = async (req: LoginRequest) => {
    const { data } = await adminApi.post<AdminAuthResponse>('/api/admin/auth/login', req);
    persistSession(data.accessToken, data.refreshToken, JSON.stringify(data.adminUser));
    setUser(data.adminUser);
  };

  const logout = () => {
    const refreshToken = getRefreshToken();
    if (refreshToken) {
      adminApi.post('/api/admin/auth/logout', { refreshToken }).catch(() => { /* ignore */ });
    }
    clearSession();
    setUser(null);
  };

  const hasRole = (...roles: string[]) => !!user && roles.some((r) => user.roles.includes(r));
  const canMutate = !!user && user.roles.some((r) => MUTATING_ROLES.includes(r));

  return (
    <Ctx.Provider value={{ user, loading, login, logout, hasRole, canMutate }}>
      {children}
    </Ctx.Provider>
  );
}

export function useAuth() {
  const v = useContext(Ctx);
  if (!v) throw new Error('useAuth fuori da AuthProvider');
  return v;
}
