// Pub/sub minimale per eventi di sessione globale.
// Il client API emette `signed-out` quando il refresh fallisce o un 401 finale
// non può essere recuperato; AuthProvider ascolta e ripulisce lo stato + naviga
// allo schermo di login.

type Handler = () => void;

const handlers = new Set<Handler>();

export const authEvents = {
  emitSignedOut(): void {
    for (const h of Array.from(handlers)) {
      try {
        h();
      } catch {
        // ignore: gli ascoltatori non devono rompere l'emitter
      }
    }
  },
  onSignedOut(handler: Handler): () => void {
    handlers.add(handler);
    return () => {
      handlers.delete(handler);
    };
  }
};
