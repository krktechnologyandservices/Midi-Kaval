import React from 'react';
import {createNativeStackNavigator} from '@react-navigation/native-stack';
import {TodayStackParamList} from './types';
import {TodayScreen} from '../screens/today/TodayScreen';
import {ActiveVisitScreen} from '../screens/today/ActiveVisitScreen';
import {CourtScheduleScreen} from '../screens/court/CourtScheduleScreen';

const Stack = createNativeStackNavigator<TodayStackParamList>();

export function TodayStackNavigator(): React.JSX.Element {
  return (
    <Stack.Navigator screenOptions={{headerShown: true}}>
      <Stack.Screen
        name="TodayHome"
        component={TodayScreen}
        options={{headerShown: false}}
      />
      <Stack.Screen
        name="ActiveVisit"
        component={ActiveVisitScreen}
        options={{title: 'Active visit'}}
      />
      <Stack.Screen
        name="CourtSchedule"
        component={CourtScheduleScreen}
        options={{title: 'Court this week'}}
      />
    </Stack.Navigator>
  );
}
