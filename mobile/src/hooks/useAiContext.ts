import { useEffect, useState } from 'react';
import { api } from '../api/client';
import { getAiStatus } from '../api/ai';
import type { AiStatus } from '../api/ai';
import type { CareCircle } from '@accanto/shared/types';

interface AiContext {
  loading: boolean;
  systemAvailable: boolean;
  enabledForCircle: boolean;
  status: AiStatus | null;
  circle: CareCircle | null;
  refresh: () => Promise<void>;
}

// Cache module-level: lo stato AI di sistema cambia raramente, evitiamo di
// rifarne fetch a ogni mount.
const statusCache: { v: AiStatus | null; at: number } = { v: null, at: 0 };
const TTL_MS = 30_000;

/**
 * Porting 1:1 di `frontend/src/hooks/useAiContext.ts`: combina lo stato AI
 * globale (cached) con (opzionale) le info di un cerchio specifico.
 */
export function useAiContext(circleId?: string): AiContext {
  const [status, setStatus] = useState<AiStatus | null>(statusCache.v);
  const [circle, setCircle] = useState<CareCircle | null>(null);
  const [loading, setLoading] = useState(true);

  const load = async () => {
    setLoading(true);
    try {
      const tasks: Array<Promise<unknown>> = [];
      const now = Date.now();
      if (!statusCache.v || now - statusCache.at > TTL_MS) {
        tasks.push(
          getAiStatus()
            .then((s) => {
              statusCache.v = s;
              statusCache.at = Date.now();
              setStatus(s);
            })
            .catch(() => {
              statusCache.v = { available: false, provider: 'none', model: '' };
              statusCache.at = Date.now();
              setStatus(statusCache.v);
            })
        );
      } else {
        setStatus(statusCache.v);
      }
      if (circleId) {
        tasks.push(
          api
            .get<CareCircle>(`/care-circles/${circleId}`)
            .then((r) => setCircle(r.data))
            .catch(() => setCircle(null))
        );
      }
      await Promise.all(tasks);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [circleId]);

  return {
    loading,
    systemAvailable: !!status?.available,
    enabledForCircle: !!circle?.aiEnabled,
    status,
    circle,
    refresh: load
  };
}
