import AsyncStorage from '@react-native-async-storage/async-storage';

/**
 * Token di invito catturato da un deep link (`accanto://invite/:token`) mentre
 * l'utente non era autenticato. Viene salvato in AsyncStorage così sopravvive
 * a kill dell'app e all'attraversamento del Login.
 *
 * Lo consuma `RootNavigator` subito dopo che `user` diventa valorizzato: in
 * quel momento naviga su `App > InviteAccept` con il token e poi pulisce.
 */
const PENDING_INVITE_KEY = 'accanto.pendingInvite';

export async function getPendingInvite(): Promise<string | null> {
  return AsyncStorage.getItem(PENDING_INVITE_KEY);
}

export async function setPendingInvite(token: string | null): Promise<void> {
  if (!token) {
    await AsyncStorage.removeItem(PENDING_INVITE_KEY);
    return;
  }
  await AsyncStorage.setItem(PENDING_INVITE_KEY, token);
}
