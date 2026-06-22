import { createBottomTabNavigator } from '@react-navigation/bottom-tabs';
import type { CircleTabParamList } from './types';
import CircleOverviewScreen from '../screens/CircleOverviewScreen';
import TimelineScreen from '../screens/TimelineScreen';
import DocumentsScreen from '../screens/DocumentsScreen';
import DoctorQuestionsScreen from '../screens/DoctorQuestionsScreen';
import SharedUpdatesScreen from '../screens/SharedUpdatesScreen';

const Tab = createBottomTabNavigator<CircleTabParamList>();

export default function CircleTabs() {
  return (
    <Tab.Navigator
      screenOptions={{
        tabBarActiveTintColor: '#0f172a',
        tabBarInactiveTintColor: '#64748b',
        headerShown: false
      }}
    >
      <Tab.Screen
        name="CircleOverview"
        component={CircleOverviewScreen}
        options={{ title: 'Panoramica' }}
      />
      <Tab.Screen
        name="Timeline"
        component={TimelineScreen}
        options={{ title: 'Diario' }}
      />
      <Tab.Screen
        name="Documents"
        component={DocumentsScreen}
        options={{ title: 'Documenti' }}
      />
      <Tab.Screen
        name="DoctorQuestions"
        component={DoctorQuestionsScreen}
        options={{ title: 'Domande' }}
      />
      <Tab.Screen
        name="SharedUpdates"
        component={SharedUpdatesScreen}
        options={{ title: 'Aggiornamenti' }}
      />
    </Tab.Navigator>
  );
}
