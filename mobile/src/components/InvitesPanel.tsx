import { useCallback, useEffect, useState } from 'react';
import { Alert, Pressable, Share, Text, View } from 'react-native';
import { useTranslation } from 'react-i18next';
import * as Clipboard from 'expo-clipboard';
import { api, extractError } from '../api/client';
import Button from './ui/Button';
import TextField from './ui/TextField';
import ErrorBanner from './ui/ErrorBanner';
import { WEB_BASE_URL } from '../config/env';
import type {
  CareCircleInvite,
  CareCircleRole,
  CreateInviteRequest
} from '@accanto/shared/types';

interface Props {
  circleId: string;
}

/**
 * Pannello di gestione inviti per il owner di un cerchio. Porting 1:1 di
 * `frontend/src/components/InvitesPanel.tsx` adattato al mobile:
 * - select → toggle segmentato (sono solo 2 ruoli: Caregiver / Viewer)
 * - input numeri → TextField number-pad
 * - "Copia link" usa expo-clipboard
 * - "Condividi link" extra rispetto al web: usa il Share nativo
 * - revoca con Alert di conferma
 */
export default function InvitesPanel({ circleId }: Props) {
  const { t } = useTranslation();
  const [invites, setInvites] = useState<CareCircleInvite[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [creating, setCreating] = useState(false);
  const [role, setRole] = useState<CareCircleRole>('Caregiver');
  const [expiresInDays, setExpiresInDays] = useState<string>('7');
  const [maxUses, setMaxUses] = useState<string>('1');
  const [copiedToken, setCopiedToken] = useState<string | null>(null);

  const load = useCallback(async () => {
    try {
      const { data } = await api.get<CareCircleInvite[]>(
        `/care-circles/${circleId}/invites`
      );
      setInvites(data);
    } catch (e) {
      setError(extractError(e));
    }
  }, [circleId]);

  useEffect(() => {
    load();
  }, [load]);

  const create = async () => {
    setCreating(true);
    setError(null);
    try {
      const days = Math.max(1, Math.min(90, Number(expiresInDays) || 1));
      const uses = Math.max(1, Math.min(50, Number(maxUses) || 1));
      const body: CreateInviteRequest = {
        role,
        expiresInDays: days,
        maxUses: uses
      };
      const { data } = await api.post<CareCircleInvite>(
        `/care-circles/${circleId}/invites`,
        body
      );
      setInvites((prev) => (prev ? [data, ...prev] : [data]));
    } catch (e) {
      setError(extractError(e));
    } finally {
      setCreating(false);
    }
  };

  const revoke = (inviteId: string) => {
    Alert.alert(
      t('circle.invites.revokeConfirmTitle'),
      t('circle.invites.revokeConfirmBody'),
      [
        { text: t('common.cancel'), style: 'cancel' },
        {
          text: t('circle.invites.revoke'),
          style: 'destructive',
          onPress: async () => {
            try {
              await api.delete(`/care-circles/${circleId}/invites/${inviteId}`);
              await load();
            } catch (e) {
              setError(extractError(e));
            }
          }
        }
      ]
    );
  };

  const inviteUrl = (token: string) => `${WEB_BASE_URL}/invite/${token}`;

  const copy = async (token: string) => {
    try {
      await Clipboard.setStringAsync(inviteUrl(token));
      setCopiedToken(token);
      setTimeout(
        () => setCopiedToken((cur) => (cur === token ? null : cur)),
        2000
      );
    } catch {
      setError(t('circle.invites.copyError'));
    }
  };

  const shareLink = async (token: string) => {
    try {
      await Share.share({
        message: t('circle.invites.shareMessage', { url: inviteUrl(token) })
      });
    } catch {
      // Utente ha annullato lo share sheet: nessun errore reale.
    }
  };

  return (
    <View className="mt-6 rounded-lg border border-accanto-100 bg-white p-4">
      <Text className="font-medium text-accanto-900">
        {t('circle.invites.title')}
      </Text>
      <Text className="text-sm text-accanto-500 mt-1">
        {t('circle.invites.subtitle')}
      </Text>

      {/* Toggle ruolo: Caregiver / Viewer */}
      <Text className="text-sm font-medium text-accanto-700 mt-4 mb-1">
        {t('circle.invites.roleLabel')}
      </Text>
      <View className="flex-row rounded-md border border-accanto-100 overflow-hidden">
        {(['Caregiver', 'Viewer'] as const).map((r) => {
          const selected = role === r;
          return (
            <Pressable
              key={r}
              onPress={() => setRole(r)}
              className={`flex-1 py-2 items-center ${
                selected ? 'bg-accanto-700' : 'bg-white'
              }`}
            >
              <Text
                className={`text-sm font-medium ${
                  selected ? 'text-white' : 'text-accanto-700'
                }`}
              >
                {t(`roles.${r.toLowerCase()}`)}
              </Text>
            </Pressable>
          );
        })}
      </View>

      <View className="flex-row gap-3 mt-3">
        <View className="flex-1">
          <TextField
            label={t('circle.invites.expiryLabel') as string}
            value={expiresInDays}
            onChangeText={setExpiresInDays}
            keyboardType="number-pad"
            maxLength={2}
          />
        </View>
        <View className="flex-1">
          <TextField
            label={t('circle.invites.maxUsesLabel') as string}
            value={maxUses}
            onChangeText={setMaxUses}
            keyboardType="number-pad"
            maxLength={2}
          />
        </View>
      </View>

      <View className="mt-3">
        <Button onPress={create} busy={creating} disabled={creating}>
          {creating ? t('circle.invites.creating') : t('circle.invites.createCta')}
        </Button>
      </View>

      <View className="mt-3">
        <ErrorBanner message={error} />
      </View>

      <View className="mt-4 gap-3">
        {invites === null ? (
          <Text className="text-sm text-accanto-500">{t('common.loading')}</Text>
        ) : invites.length === 0 ? (
          <Text className="text-sm text-accanto-500">
            {t('circle.invites.empty')}
          </Text>
        ) : (
          invites.map((i) => (
            <InviteRow
              key={i.id}
              invite={i}
              url={inviteUrl(i.token)}
              copied={copiedToken === i.token}
              onCopy={() => copy(i.token)}
              onShare={() => shareLink(i.token)}
              onRevoke={() => revoke(i.id)}
            />
          ))
        )}
      </View>
    </View>
  );
}

function InviteRow({
  invite,
  url,
  copied,
  onCopy,
  onShare,
  onRevoke
}: {
  invite: CareCircleInvite;
  url: string;
  copied: boolean;
  onCopy: () => void;
  onShare: () => void;
  onRevoke: () => void;
}) {
  const { t, i18n } = useTranslation();
  const statusKey = invite.revokedAt
    ? 'revoked'
    : !invite.isActive
    ? new Date(invite.expiresAt) <= new Date()
      ? 'expired'
      : 'exhausted'
    : 'active';
  const status = t(`circle.invites.status.${statusKey}`) as string;
  const expires = new Date(invite.expiresAt).toLocaleDateString(i18n.language, {
    day: '2-digit',
    month: 'long',
    year: 'numeric'
  });
  const statusActive = statusKey === 'active';

  return (
    <View className="border border-accanto-100 rounded-md p-3">
      <View className="flex-row items-center justify-between gap-2">
        <Text className="text-sm text-accanto-900 flex-1" numberOfLines={2}>
          <Text className="font-semibold">
            {t(`roles.${invite.role.toLowerCase()}`)}
          </Text>
          {t('circle.invites.rowMeta', {
            date: expires,
            used: invite.usedCount,
            max: invite.maxUses
          })}
        </Text>
        <View
          className={`px-2 py-0.5 rounded-full ${
            statusActive
              ? 'bg-accanto-50 border border-accanto-100'
              : 'bg-accanto-100'
          }`}
        >
          <Text
            className={`text-xs ${
              statusActive ? 'text-accanto-700' : 'text-accanto-500'
            }`}
          >
            {status}
          </Text>
        </View>
      </View>

      {invite.isActive ? (
        <>
          <View className="mt-2 rounded-md bg-accanto-50 px-2 py-1">
            <Text
              className="text-xs text-accanto-500 font-mono"
              numberOfLines={2}
            >
              {url}
            </Text>
          </View>
          <View className="mt-2 flex-row flex-wrap gap-2">
            <View className="flex-1 min-w-[100px]">
              <Button variant="ghost" onPress={onCopy}>
                {copied ? t('common.copied') : t('circle.invites.copyLink')}
              </Button>
            </View>
            <View className="flex-1 min-w-[100px]">
              <Button variant="ghost" onPress={onShare}>
                {t('common.share')}
              </Button>
            </View>
            <View className="flex-1 min-w-[100px]">
              <Button variant="ghost" onPress={onRevoke}>
                {t('circle.invites.revoke')}
              </Button>
            </View>
          </View>
        </>
      ) : null}
    </View>
  );
}
