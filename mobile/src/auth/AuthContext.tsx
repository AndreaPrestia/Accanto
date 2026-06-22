import {
  createContext,
  useContext,
  useEffect,
  useState,
  ReactNode,
  useCallback
} from 'react';
import { api } from '../api/client';
import i18n, { persistLanguage } from '../i18n';
import {
  clearRefreshToken,
  clearToken,
  getRefreshToken,
  setRefreshToken,
  setToken
} from '../storage/secureStorage';
import {
  clearStoredUser,
  getStoredUser,
  setStoredUser
} from '../storage/userStorage';
import { authEvents } from './events';
import {
  authenticateBiometric,
  isBiometricEnabled
} from './biometric';
import type {
  AuthResponse,
  LoginRequest,
  LoginResult,
  RegisterRequest,
  User
} from '@accanto/shared/types';
import { SUPPORTED_LANGUAGES } from '@accanto/shared/i18n/constants';

interface AuthCtx {
  user: User | null;
  loading: boolean;
  /** True quando l'utente è loggato ma deve sbloccare con biometria. */
  needsBiometricUnlock: boolean;
  login: (req: LoginRequest) => Promise<LoginResult>;
  completeTwoFactor: (
    twoFactorToken: string,
    code?: string,
    recoveryCode?: string
  ) => Promise<void>;
  register: (req: RegisterRequest) => Promise<void>;
  logout: () => Promise<void>;
  setLanguage: (lang: string) => Promise<void>;
  unlockBiometric: () => Promise<boolean>;
}

const Ctx = createContext<AuthCtx | undefined>(undefined);

const supportedLangs = SUPPORTED_LANGUAGES as readonly string[];

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(null);
  const [loading, setLoading] = useState(true);
  const [needsBiometricUnlock, setNeedsBiometricUnlock] = useState(false);

  // Idratazione iniziale: legge user + token + flag biometrico.
  useEffect(() => {
    (async () => {
      try {
        const storedUser = await getStoredUser();
        const refresh = await getRefreshToken();
        if (storedUser && refresh) {
          if (
            storedUser.language &&
            supportedLangs.includes(storedUser.language)
          ) {
            await i18n.changeLanguage(storedUser.language);
          }
          const bioOn = await isBiometricEnabled();
          if (bioOn) {
            setNeedsBiometricUnlock(true);
          }
          setUser(storedUser);
        }
      } finally {
        setLoading(false);
      }
    })();
  }, []);

  // Reagisce a sign-out emessi dall'API client (refresh fallito o 401 finale).
  useEffect(() => {
    const off = authEvents.onSignedOut(() => {
      setUser(null);
      setNeedsBiometricUnlock(false);
    });
    return off;
  }, []);

  const persist = useCallback(async (res: AuthResponse) => {
    await setToken(res.accessToken);
    if (res.refreshToken) await setRefreshToken(res.refreshToken);
    await setStoredUser(res.user);
    if (
      res.user.language &&
      supportedLangs.includes(res.user.language)
    ) {
      await i18n.changeLanguage(res.user.language);
    }
    setUser(res.user);
    setNeedsBiometricUnlock(false);
  }, []);

  const login = useCallback(
    async (req: LoginRequest): Promise<LoginResult> => {
      const { data } = await api.post<LoginResult>('/auth/login', req);
      if (!data.requiresTwoFactor && data.auth) {
        await persist(data.auth);
      }
      return data;
    },
    [persist]
  );

  const completeTwoFactor = useCallback(
    async (twoFactorToken: string, code?: string, recoveryCode?: string) => {
      const { data } = await api.post<AuthResponse>('/auth/two-factor', {
        twoFactorToken,
        code: code ?? null,
        recoveryCode: recoveryCode ?? null
      });
      await persist(data);
    },
    [persist]
  );

  const register = useCallback(
    async (req: RegisterRequest) => {
      const { data } = await api.post<AuthResponse>('/auth/register', req);
      await persist(data);
    },
    [persist]
  );

  const logout = useCallback(async () => {
    const refreshToken = await getRefreshToken();
    if (refreshToken) {
      // Fire-and-forget: revoca lato server senza bloccare l'UI.
      api.post('/auth/logout', { refreshToken }).catch(() => {
        /* ignore */
      });
    }
    await clearToken();
    await clearRefreshToken();
    await clearStoredUser();
    setUser(null);
    setNeedsBiometricUnlock(false);
  }, []);

  const setLanguage = useCallback(async (lang: string) => {
    if (!supportedLangs.includes(lang)) return;
    await persistLanguage(lang);
    setUser((prev) => {
      if (!prev) return prev;
      const next = { ...prev, language: lang };
      setStoredUser(next).catch(() => {
        /* best effort */
      });
      return next;
    });
  }, []);

  const unlockBiometric = useCallback(async (): Promise<boolean> => {
    const r = await authenticateBiometric({
      promptMessage: 'Sblocca Accanto'
    });
    if (r.success) {
      setNeedsBiometricUnlock(false);
      return true;
    }
    return false;
  }, []);

  return (
    <Ctx.Provider
      value={{
        user,
        loading,
        needsBiometricUnlock,
        login,
        completeTwoFactor,
        register,
        logout,
        setLanguage,
        unlockBiometric
      }}
    >
      {children}
    </Ctx.Provider>
  );
}

export function useAuth(): AuthCtx {
  const v = useContext(Ctx);
  if (!v) throw new Error('useAuth fuori da AuthProvider');
  return v;
}
