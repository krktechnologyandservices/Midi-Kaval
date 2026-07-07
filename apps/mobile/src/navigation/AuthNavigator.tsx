import React from 'react';
import {createNativeStackNavigator} from '@react-navigation/native-stack';
import {ForgotPasswordScreen} from '../screens/auth/ForgotPasswordScreen';
import {ResetPasswordScreen} from '../screens/auth/ResetPasswordScreen';
import {LoginScreen} from '../screens/auth/LoginScreen';
import {OtpScreen} from '../screens/auth/OtpScreen';
import {TotpLoginScreen} from '../screens/auth/TotpLoginScreen';
import {SessionExpiredScreen} from '../screens/auth/SessionExpiredScreen';
import {AuthStackParamList} from './types';

const Stack = createNativeStackNavigator<AuthStackParamList>();

interface AuthNavigatorProps {
  initialRouteName?: keyof AuthStackParamList;
}

export function AuthNavigator({
  initialRouteName = 'Login',
}: AuthNavigatorProps): React.JSX.Element {
  return (
    <Stack.Navigator
      initialRouteName={initialRouteName}
      screenOptions={{headerShown: false}}>
      <Stack.Screen name="Login" component={LoginScreen} />
      <Stack.Screen name="ForgotPassword" component={ForgotPasswordScreen} />
      <Stack.Screen name="ResetPassword" component={ResetPasswordScreen} />
      <Stack.Screen name="Otp" component={OtpScreen} />
      <Stack.Screen name="Totp" component={TotpLoginScreen} />
      <Stack.Screen name="SessionExpired" component={SessionExpiredScreen} />
    </Stack.Navigator>
  );
}
