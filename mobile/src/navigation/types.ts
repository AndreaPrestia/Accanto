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

// ---- MainStack (native stack interno al Drawer: contiene Dashboard, ------
// flussi push come Circle / NewCircle / accettazione invito) ---------------
export type MainStackParamList = {
  Dashboard: undefined;
  NewCircle: undefined;
  Circle: { circleId: string };
  InviteAccept: { token: string };
};

// ---- AppDrawer (root per utenti autenticati) ------------------------------
// "Main" ospita il MainStack (che a sua volta apre il singolo cerchio).
// Le altre voci sono pagine "globali" accessibili dal drawer da qualunque
// punto, anche da dentro un cerchio (l'hamburger viene reso in MainStack
// e in CircleStack via getParent('AppDrawer').openDrawer()).
export type AppDrawerParamList = {
  Main: NavigatorScreenParams<MainStackParamList> | undefined;
  Account: undefined;
  AiHistory: undefined;
  Support: undefined;
  SelfCare: undefined;
};

// ---- RootStack (auth vs app) ----------------------------------------------
export type RootStackParamList = {
  Auth: NavigatorScreenParams<AuthStackParamList> | undefined;
  App: NavigatorScreenParams<AppDrawerParamList> | undefined;
};

// ---- Helper screen prop types ---------------------------------------------
export type AuthScreenProps<T extends keyof AuthStackParamList> =
  NativeStackScreenProps<AuthStackParamList, T>;

// Schermi dentro al MainStack: ricevono navigation con accesso composito
// al MainStack + drawer parente (utile per `navigation.openDrawer()` o
// per navigare a sibling drawer-screen come 'Account').
export type AppScreenProps<T extends keyof MainStackParamList> =
  CompositeScreenProps<
    NativeStackScreenProps<MainStackParamList, T>,
    DrawerScreenProps<AppDrawerParamList>
  >;

export type DrawerScreen<T extends keyof AppDrawerParamList> =
  DrawerScreenProps<AppDrawerParamList, T>;

export type CircleStackScreen<T extends keyof CircleStackParamList> =
  CompositeScreenProps<
    NativeStackScreenProps<CircleStackParamList, T>,
    AppScreenProps<keyof MainStackParamList>
  >;

export type CircleTabScreen<T extends keyof CircleTabParamList> =
  CompositeScreenProps<
    BottomTabScreenProps<CircleTabParamList, T>,
    CircleStackScreen<keyof CircleStackParamList>
  >;

