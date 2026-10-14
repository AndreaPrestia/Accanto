import { createBottomTabNavigator } from '@react-navigation/bottom-tabs';
import { Ionicons } from '@expo/vector-icons';
import { useTranslation } from 'react-i18next';
import type { CircleTabParamList } from './types';
import CircleOverviewScreen from '../screens/CircleOverviewScreen';
import TimelineScreen from '../screens/TimelineScreen';
import DocumentsScreen from '../screens/DocumentsScreen';
import DoctorQuestionsScreen from '../screens/DoctorQuestionsScreen';
import SharedUpdatesScreen from '../screens/SharedUpdatesScreen';

const Tab = createBottomTabNavigator<CircleTabParamList>();

type IoniconName = keyof typeof Ionicons.glyphMap;

const TAB_ICON: Record<keyof CircleTabParamList, IoniconName> = {
  CircleOverview: 'grid-outline',
  Timeline: 'book-outline',
  Documents: 'document-text-outline',
  DoctorQuestions: 'help-circle-outline',
  SharedUpdates: 'megaphone-outline'
};

export default function CircleTabs() {
  const { t } = useTranslation();
  return (
    <Tab.Navigator
      screenOptions={({ route }) => ({
        tabBarActiveTintColor: '#0f172a',
        tabBarInactiveTintColor: '#64748b',
        headerShown: false,
        tabBarIcon: ({ color, size }) => (
          <Ionicons
            name={TAB_ICON[route.name as keyof CircleTabParamList]}
            size={size}
            color={color}
          />
        )
      })}
    >
      <Tab.Screen
        name="CircleOverview"
        component={CircleOverviewScreen}
        options={{ title: t('nav.overview') }}
      />
      <Tab.Screen
        name="Timeline"
        component={TimelineScreen}
        options={{ title: t('nav.timeline') }}
      />
      <Tab.Screen
        name="Documents"
        component={DocumentsScreen}
        options={{ title: t('nav.documents') }}
      />
      <Tab.Screen
        name="DoctorQuestions"
        component={DoctorQuestionsScreen}
        options={{ title: t('nav.questions') }}
      />
      <Tab.Screen
        name="SharedUpdates"
        component={SharedUpdatesScreen}
        options={{ title: t('nav.updates') }}
      />
    </Tab.Navigator>
  );
}
