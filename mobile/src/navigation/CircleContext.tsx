import { createContext, useContext, ReactNode } from 'react';

interface CircleCtx {
  circleId: string;
}

const Ctx = createContext<CircleCtx | null>(null);

export function CircleProvider({
  circleId,
  children
}: {
  circleId: string;
  children: ReactNode;
}) {
  return <Ctx.Provider value={{ circleId }}>{children}</Ctx.Provider>;
}

/**
 * Restituisce il `circleId` del cerchio corrente. Disponibile in qualunque
 * screen montato sotto `CircleStack` (tabs incluse). Throws se chiamato
 * fuori dal provider, perch\u00e9 indica un bug di nesting.
 */
export function useCircleId(): string {
  const v = useContext(Ctx);
  if (!v) throw new Error('useCircleId() usato fuori da CircleProvider');
  return v.circleId;
}

/**
 * Variante "soft" di useCircleId: ritorna `null` quando lo screen \u00e8 fuori
 * dal CircleProvider (es. AiHistoryScreen aperto dal drawer globale).
 */
export function useOptionalCircleId(): string | null {
  const v = useContext(Ctx);
  return v?.circleId ?? null;
}
