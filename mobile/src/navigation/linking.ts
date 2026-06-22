import * as Linking from 'expo-linking';
import type { LinkingOptions } from '@react-navigation/native';
import type { RootStackParamList } from './types';

// Schema:
//   accanto://invite/:token              -> InviteAccept (può essere in Auth o App)
//   accanto://reset-password?token=...   -> ResetPassword
//   accanto://care-circles/:circleId     -> Circle (richiede auth)
//   accanto://care-circles/:circleId/timeline | documents | ...
//   accanto://self-care | /support       -> public
//
// Universal/App Links: https://accanto.app/... (configurato in app.config.ts).

export const linking: LinkingOptions<RootStackParamList> = {
  prefixes: [Linking.createURL('/'), 'accanto://', 'https://accanto.app'],
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
