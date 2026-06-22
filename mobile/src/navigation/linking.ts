import * as Linking from 'expo-linking';
import type { LinkingOptions } from '@react-navigation/native';
import type { RootStackParamList } from './types';

// Schema:
//   <scheme>://invite/:token              -> InviteAccept (può essere in Auth o App)
//   <scheme>://reset-password?token=...   -> ResetPassword
//   <scheme>://care-circles/:circleId     -> Circle (richiede auth)
//   <scheme>://care-circles/:circleId/timeline | documents | ...
//   <scheme>://self-care | /support       -> public
//
// <scheme> dipende dalla variant (vedi app.config.ts):
//   production  → accanto://
//   preview     → accanto.preview://
//   development → accanto.dev://
// Linking.createURL('/') costruisce il prefisso giusto a runtime in
// base allo scheme dichiarato in app.config.ts.
//
// Universal/App Links: https://accanto.care/... — funziona solo sulle
// build production firmate con il cert riportato in assetlinks.json /
// apple-app-site-association serviti da web/Astro.

export const linking: LinkingOptions<RootStackParamList> = {
  prefixes: [Linking.createURL('/'), 'https://accanto.care'],
  config: {
    screens: {
      Auth: {
        screens: {
          Login: 'login',
          Register: 'register',
          ForgotPassword: 'forgot-password',
          ResetPassword: 'reset-password',
          InviteAccept: 'invite/:token',
          Support: 'support',
          SelfCare: 'self-care'
        }
      },
      App: {
        screens: {
          NewCircle: 'care-circles/new',
          InviteAccept: 'invite/:token',
          AppDrawer: {
            screens: {
              Dashboard: '',
              Account: 'account',
              AiHistory: 'ai/history',
              Support: 'support',
              SelfCare: 'self-care'
            }
          },
          Circle: {
            path: 'care-circles/:circleId',
            screens: {
              CircleTabs: {
                screens: {
                  CircleOverview: '',
                  Timeline: 'timeline',
                  Documents: 'documents',
                  DoctorQuestions: 'questions',
                  SharedUpdates: 'shared-updates'
                }
              },
              DifficultDay: 'difficult-day',
              Audit: 'audit',
              AiHistoryCircle: 'ai/history'
            }
          }
        }
      }
    }
  }
};
