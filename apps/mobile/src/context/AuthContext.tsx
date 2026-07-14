import React, {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
} from 'react';
import {
  authSessionService,
  AuthSessionService,
} from '../services/auth/AuthSessionService';
import {deviceRegistrationService} from '../services/devices/DeviceRegistrationService';
import {
  isFieldRole,
  isSupervisorRole,
  resolveAuthDestination,
} from '../services/auth/roleRouting';
import {
  LoginRequest,
  OtpChallengeState,
  SessionUserDto,
  TotpChallengeState,
} from '../services/auth/auth.models';

export type AuthPhase = 'loading' | 'ready';

interface AuthContextValue {
  phase: AuthPhase;
  isAuthenticated: boolean;
  user: SessionUserDto | null;
  otpChallenge: OtpChallengeState | null;
  totpChallenge: TotpChallengeState | null;
  sessionExpired: boolean;
  destination: ReturnType<typeof resolveAuthDestination>;
  isFieldRole: boolean;
  isSupervisorRole: boolean;
  login: (request: LoginRequest) => Promise<{requiresTotp: boolean}>;
  verifyOtp: (code: string) => Promise<void>;
  clearOtpChallenge: () => Promise<void>;
  verifyTotpLogin: (code: string) => Promise<void>;
  forgotPassword: (email: string) => Promise<{message?: string}>;
  resetPassword: (token: string, newPassword: string) => Promise<{message?: string}>;
  logout: () => Promise<void>;
  clearSessionExpired: () => void;
  extractErrorMessage: (error: unknown) => string;
}

const AuthContext = createContext<AuthContextValue | null>(null);

interface AuthProviderProps {
  children: React.ReactNode;
  service?: AuthSessionService;
}

export function AuthProvider({
  children,
  service = authSessionService,
}: AuthProviderProps): React.JSX.Element {
  const [phase, setPhase] = useState<AuthPhase>('loading');
  const [isAuthenticated, setIsAuthenticated] = useState(false);
  const [user, setUser] = useState<SessionUserDto | null>(null);
  const [otpChallenge, setOtpChallenge] = useState<OtpChallengeState | null>(
    null,
  );
  const [totpChallenge, setTotpChallenge] = useState<TotpChallengeState | null>(
    null,
  );
  const [sessionExpired, setSessionExpired] = useState(false);

  const syncFromService = useCallback(async () => {
    const authenticated = await service.isAuthenticated();
    setIsAuthenticated(authenticated);
    setUser(service.getUser());
    setOtpChallenge(service.getOtpChallenge());
    setTotpChallenge(service.getTotpChallenge());
  }, [service]);

  useEffect(() => {
    service.onSessionExpired = () => {
      setSessionExpired(true);
      setIsAuthenticated(false);
      setUser(null);
      setOtpChallenge(null);
      setTotpChallenge(null);
    };

    service.onDeactivated = () => {
      setSessionExpired(false);
      setIsAuthenticated(false);
      setUser(null);
      setOtpChallenge(null);
      setTotpChallenge(null);
    };

    return () => {
      service.onSessionExpired = null;
      service.onDeactivated = null;
    };
  }, [service]);

  useEffect(() => {
    let active = true;

    (async () => {
      await service.bootstrapSession();
      if (!active) {
        return;
      }
      await syncFromService();
      setPhase('ready');
    })();

    return () => {
      active = false;
    };
  }, [service, syncFromService]);

  const login = useCallback(
    async (request: LoginRequest) => {
      const response = await service.login(request);
      setSessionExpired(false);
      await syncFromService();
      return {requiresTotp: !!(response as unknown as Record<string, unknown>)['requiresTotp']};
    },
    [service, syncFromService],
  );

  const verifyOtp = useCallback(
    async (code: string) => {
      await service.verifyOtp(code);
      setSessionExpired(false);
      await syncFromService();
      void deviceRegistrationService.registerIfAuthenticated();
    },
    [service, syncFromService],
  );

  const clearOtpChallenge = useCallback(async () => {
    await service.clearOtpChallenge();
    setOtpChallenge(null);
  }, [service]);

  const verifyTotpLogin = useCallback(
    async (code: string) => {
      await service.verifyTotpLogin(code);
      setSessionExpired(false);
      await syncFromService();
      void deviceRegistrationService.registerIfAuthenticated();
    },
    [service, syncFromService],
  );

  const forgotPassword = useCallback(
    async (email: string) => service.forgotPassword(email),
    [service],
  );

  const resetPassword = useCallback(
    async (token: string, newPassword: string) =>
      service.resetPassword(token, newPassword),
    [service],
  );

  const logout = useCallback(async () => {
    await service.logout();
    setSessionExpired(false);
    setIsAuthenticated(false);
    setUser(null);
    setOtpChallenge(null);
    setTotpChallenge(null);
  }, [service]);

  const clearSessionExpired = useCallback(() => {
    setSessionExpired(false);
  }, []);

  const destination = useMemo(() => {
    if (sessionExpired) {
      return 'auth' as const;
    }
    return resolveAuthDestination(isAuthenticated, user?.role);
  }, [isAuthenticated, sessionExpired, user?.role]);

  const value = useMemo<AuthContextValue>(
    () => ({
      phase,
      isAuthenticated,
      user,
      otpChallenge,
      totpChallenge,
      sessionExpired,
      destination,
      isFieldRole: isFieldRole(user?.role),
      isSupervisorRole: isSupervisorRole(user?.role),
      login,
      verifyOtp,
      clearOtpChallenge,
      verifyTotpLogin,
      forgotPassword,
      resetPassword,
      logout,
      clearSessionExpired,
      extractErrorMessage: (error: unknown) => service.extractErrorMessage(error),
    }),
    [
      phase,
      isAuthenticated,
      user,
      otpChallenge,
      totpChallenge,
      sessionExpired,
      destination,
      login,
      verifyOtp,
      clearOtpChallenge,
      verifyTotpLogin,
      forgotPassword,
      resetPassword,
      logout,
      clearSessionExpired,
      service,
    ],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth must be used within AuthProvider');
  }
  return context;
}
