import { createNativeStackNavigator } from '@react-navigation/native-stack';
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
        options={{ title: 'Crea account' }}
      />
      <Stack.Screen
        name="ForgotPassword"
        component={ForgotPasswordScreen}
        options={{ title: 'Password dimenticata' }}
      />
      <Stack.Screen
        name="ResetPassword"
        component={ResetPasswordScreen}
        options={{ title: 'Reimposta password' }}
      />
      <Stack.Screen
        name="InviteAccept"
        component={InviteAcceptScreen}
        options={{ title: 'Invito' }}
      />
      <Stack.Screen
        name="Support"
        component={SupportScreen}
        options={{ title: 'Supporto' }}
      />
      <Stack.Screen
        name="SelfCare"
        component={SelfCareScreen}
        options={{ title: 'Prenditi cura di te' }}
      />
    </Stack.Navigator>
  );
}
