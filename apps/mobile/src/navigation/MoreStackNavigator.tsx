import React from 'react';
import {createNativeStackNavigator} from '@react-navigation/native-stack';
import {MoreStackParamList} from './types';
import {MoreScreen} from '../screens/more/MoreScreen';
import {SyncQueueScreen} from '../screens/more/SyncQueueScreen';
import {TravelClaimFormScreen} from '../screens/travel/TravelClaimFormScreen';
import {TravelClaimsListScreen} from '../screens/travel/TravelClaimsListScreen';
import {NotificationsListScreen} from '../screens/notifications/NotificationsListScreen';

const Stack = createNativeStackNavigator<MoreStackParamList>();

export function MoreStackNavigator(): React.JSX.Element {
  return (
    <Stack.Navigator>
      <Stack.Screen name="MoreHome" component={MoreScreen} options={{title: 'More'}} />
      <Stack.Screen
        name="NotificationsList"
        component={NotificationsListScreen}
        options={{title: 'Notifications'}}
      />
      <Stack.Screen
        name="TravelClaimsList"
        component={TravelClaimsListScreen}
        options={{title: 'Travel'}}
      />
      <Stack.Screen
        name="TravelClaimForm"
        component={TravelClaimFormScreen}
        options={{title: 'Travel claim'}}
      />
      <Stack.Screen
        name="SyncQueue"
        component={SyncQueueScreen}
        options={{title: 'Sync queue'}}
      />
    </Stack.Navigator>
  );
}
