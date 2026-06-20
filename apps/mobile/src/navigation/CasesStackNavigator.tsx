import React from 'react';
import {createNativeStackNavigator} from '@react-navigation/native-stack';
import {CasesStackParamList} from './types';
import {CasesListScreen} from '../screens/cases/CasesListScreen';
import {CaseCreateScreen} from '../screens/cases/CaseCreateScreen';
import {CaseDetailPlaceholderScreen} from '../screens/cases/CaseDetailPlaceholderScreen';

const Stack = createNativeStackNavigator<CasesStackParamList>();

export function CasesStackNavigator(): React.JSX.Element {
  return (
    <Stack.Navigator>
      <Stack.Screen name="CasesList" component={CasesListScreen} options={{title: 'Cases'}} />
      <Stack.Screen name="CaseCreate" component={CaseCreateScreen} options={{title: 'New case'}} />
      <Stack.Screen
        name="CaseDetailPlaceholder"
        component={CaseDetailPlaceholderScreen}
        options={{title: 'Case detail'}}
      />
    </Stack.Navigator>
  );
}
