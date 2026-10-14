import { createNativeStackNavigator } from '@react-navigation/native-stack';
import { useTranslation } from 'react-i18next';
import type { AuthStackParamList } from './types';
import LoginScreen from '../screens/LoginScreen';
import RegisterScreen from '../screens/RegisterScreen';
import ForgotPasswordScreen from '../screens/ForgotPasswordScreen';
import ResetPasswordScreen from '../screens/ResetPasswordScreen';
import InviteAcceptScreen from '../screens/InviteAcceptScreen';
import SupportScreen from '../screens/SupportScreen';
import SelfCareScreen from '../screens/SelfCareScreen';

const Stack = createNativeStackNavigator<AuthStackParamList>();

export default function AuthStack() {
  const { t } = useTranslation();
  return (
    <Stack.Navigator
      initialRouteName="Login"
      screenOptions={{ headerShown: true, headerTitleAlign: 'center' }}
    >
      <Stack.Screen
        name="Login"
        component={LoginScreen}
        options={{ title: 'Accanto', headerShown: false }}
      />
      <Stack.Screen
        name="Register"
        component={RegisterScreen}
        options={{ title: t('nav.register') }}
      />
      <Stack.Screen
        name="ForgotPassword"
        component={ForgotPasswordScreen}
        options={{ title: t('nav.forgotPassword') }}
      />
      <Stack.Screen
        name="ResetPassword"
        component={ResetPasswordScreen}
        options={{ title: t('nav.resetPassword') }}
      />
      <Stack.Screen
        name="InviteAccept"
        component={InviteAcceptScreen}
        options={{ title: t('nav.invite') }}
      />
      <Stack.Screen
        name="Support"
        component={SupportScreen}
        options={{ title: t('nav.support') }}
      />
      <Stack.Screen
        name="SelfCare"
        component={SelfCareScreen}
        options={{ title: t('nav.selfCare') }}
      />
    </Stack.Navigator>
  );
}
