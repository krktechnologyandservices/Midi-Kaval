import React from 'react';
import {createBottomTabNavigator} from '@react-navigation/bottom-tabs';
import {TodayStackNavigator} from './TodayStackNavigator';
import {MoreStackNavigator} from './MoreStackNavigator';
import {CasesStackNavigator} from './CasesStackNavigator';
import {MainTabParamList} from './types';
import {UnreadCountProvider, useUnreadCount} from '../services/notifications/UnreadCountContext';

const Tab = createBottomTabNavigator<MainTabParamList>();

function TabNavigatorInner(): React.JSX.Element {
  const {unreadCount} = useUnreadCount();

  return (
    <Tab.Navigator
      screenOptions={{
        headerShown: true,
        tabBarActiveTintColor: '#0D6E6E',
      }}>
      <Tab.Screen
        name="Today"
        component={TodayStackNavigator}
        options={{headerShown: false}}
      />
      <Tab.Screen name="Cases" component={CasesStackNavigator} options={{headerShown: false}} />
      <Tab.Screen
        name="More"
        component={MoreStackNavigator}
        options={{
          headerShown: false,
          tabBarBadge: unreadCount > 0 ? unreadCount : undefined,
        }}
      />
    </Tab.Navigator>
  );
}

export function MainTabNavigator(): React.JSX.Element {
  return (
    <UnreadCountProvider>
      <TabNavigatorInner />
    </UnreadCountProvider>
  );
}
