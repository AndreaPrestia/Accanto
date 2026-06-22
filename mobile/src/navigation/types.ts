import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import type { CompositeScreenProps, NavigatorScreenParams } from '@react-navigation/native';
import type { DrawerScreenProps } from '@react-navigation/drawer';
import type { BottomTabScreenProps } from '@react-navigation/bottom-tabs';

// ---- AuthStack (utente NON autenticato) -----------------------------------
export type AuthStackParamList = {
  Login: { returnTo?: string } | undefined;
  Register: { returnTo?: string } | undefined;
  ForgotPassword: undefined;
  ResetPassword: { token?: string } | undefined;
  InviteAccept: { token: string };
  Support: undefined;
  SelfCare: undefined;
};

// ---- CircleTabs (tab bar interno a una care circle) -----------------------
export type CircleTabParamList = {
  CircleOverview: undefined;
  Timeline: undefined;
  Documents: undefined;
  DoctorQuestions: undefined;
  SharedUpdates: undefined;
};

// ---- CircleStack (stack che ospita CircleTabs + sotto-pagine) -------------
export type CircleStackParamList = {
  CircleTabs: NavigatorScreenParams<CircleTabParamList> | undefined;
  DifficultDay: undefined;
  Audit: undefined;
  AiHistoryCircle: undefined;
};

// ---- AppDrawer (utente autenticato) ---------------------------------------
export type AppDrawerParamList = {
  Dashboard: undefined;
  Account: undefined;
  AiHistory: undefined;
  Support: undefined;
  SelfCare: undefined;
};

// ---- AppStack (root per utenti autenticati: drawer + sotto-flussi) --------
export type AppStackParamList = {
  AppDrawer: NavigatorScreenParams<AppDrawerParamList> | undefined;
  NewCircle: undefined;
  Circle: { circleId: string };
  InviteAccept: { token: string };
};

// ---- RootStack (auth vs app) ----------------------------------------------
export type RootStackParamList = {
  Auth: NavigatorScreenParams<AuthStackParamList> | undefined;
  App: NavigatorScreenParams<AppStackParamList> | undefined;
};

// ---- Helper screen prop types ---------------------------------------------
export type AuthScreenProps<T extends keyof AuthStackParamList> =
  NativeStackScreenProps<AuthStackParamList, T>;

export type AppScreenProps<T extends keyof AppStackParamList> =
  NativeStackScreenProps<AppStackParamList, T>;

export type DrawerScreen<T extends keyof AppDrawerParamList> =
  CompositeScreenProps<
    DrawerScreenProps<AppDrawerParamList, T>,
    NativeStackScreenProps<AppStackParamList>
  >;

export type CircleStackScreen<T extends keyof CircleStackParamList> =
  CompositeScreenProps<
    NativeStackScreenProps<CircleStackParamList, T>,
    NativeStackScreenProps<AppStackParamList>
  >;

export type CircleTabScreen<T extends keyof CircleTabParamList> =
  CompositeScreenProps<
    BottomTabScreenProps<CircleTabParamList, T>,
    CircleStackScreen<keyof CircleStackParamList>
  >;
