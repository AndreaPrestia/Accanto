import { useMemo, useState } from 'react';
import { Linking, Pressable, Text, View } from 'react-native';
import { useTranslation } from 'react-i18next';
import Screen from '../components/ui/Screen';
import {
  SUPPORT_CATEGORIES,
  SUPPORT_RESOURCES,
  type SupportCategory,
  type SupportResource
} from '@accanto/shared/data/supportResources';

/**
 * Catalogo di servizi di supporto (telefono, web). Lista completa
 * importata da `@accanto/shared` e identica al web: filtrabile per
 * categoria, telefono apre il dialer (`tel:`) via Linking, URL apre il
 * browser di sistema.
 */
export default function SupportScreen() {
  const { t } = useTranslation();
  const [active, setActive] = useState<SupportCategory | 'all'>('all');

  const items = useMemo<SupportResource[]>(() => {
    if (active === 'all') return SUPPORT_RESOURCES;
    return SUPPORT_RESOURCES.filter((r) => r.category === active);
  }, [active]);

  return (
    <Screen>
      <Text className="text-2xl font-semibold text-accanto-900 mb-1">
        {t('support.title') as string}
      </Text>
      <Text className="text-accanto-500 mb-4">
        {t('support.intro') as string}
      </Text>

      <View className="flex-row flex-wrap gap-2 mb-4">
        <CategoryChip
          active={active === 'all'}
          onPress={() => setActive('all')}
          label={t('support.categories.all') as string}
        />
        {SUPPORT_CATEGORIES.map((cat) => (
          <CategoryChip
            key={cat}
            active={active === cat}
            onPress={() => setActive(cat)}
            label={t(`support.categories.${cat}`) as string}
          />
        ))}
      </View>

      <View className="gap-3">
        {items.map((r) => (
          <ResourceCard key={r.id} resource={r} t={t} />
        ))}
      </View>

      <Text className="mt-6 text-xs text-accanto-500">
        {t('support.disclaimer') as string}
      </Text>
    </Screen>
  );
}

function CategoryChip({
  active,
  onPress,
  label
}: {
  active: boolean;
  onPress: () => void;
  label: string;
}) {
  return (
    <Pressable
      onPress={onPress}
      className={`rounded-full px-3 py-1 border ${
        active
          ? 'bg-accanto-700 border-accanto-700'
          : 'bg-white border-accanto-200'
      }`}
    >
      <Text
        className={`text-sm ${
          active ? 'text-white' : 'text-accanto-700'
        }`}
      >
        {label}
      </Text>
    </Pressable>
  );
}

function ResourceCard({
  resource,
  t
}: {
  resource: SupportResource;
  t: (key: string) => string;
}) {
  return (
    <View className="rounded-lg border border-accanto-100 bg-white p-4">
      <View className="flex-row items-start justify-between gap-2">
        <Text className="font-semibold text-accanto-900 flex-1">
          {resource.name}
        </Text>
        <Text className="text-xs text-accanto-500">
          {t(`support.categories.${resource.category}`)}
        </Text>
      </View>
      <Text className="text-sm text-accanto-700 mt-1">
        {resource.description}
      </Text>
      {resource.hours ? (
        <Text className="text-xs text-accanto-500 mt-1">
          <Text className="font-medium">{t('support.hours')}:</Text>{' '}
          {resource.hours}
        </Text>
      ) : null}
      <View className="mt-2 flex-row flex-wrap gap-4">
        {resource.phone ? (
          <Pressable
            onPress={() => Linking.openURL(`tel:${resource.phone}`)}
          >
            <Text className="text-sm text-accanto-700 underline">
              {t('support.call')}: {resource.phoneLabel ?? resource.phone}
            </Text>
          </Pressable>
        ) : null}
        {resource.url ? (
          <Pressable onPress={() => Linking.openURL(resource.url!)}>
            <Text className="text-sm text-accanto-700 underline">
              {t('support.website')} ↗
            </Text>
          </Pressable>
        ) : null}
      </View>
    </View>
  );
}
