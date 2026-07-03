// Giorno dell'anno (1-366) per la rotazione deterministica dei tip.
// Implementazione 1:1 con quella inline di frontend/src/pages/SelfCarePage.tsx.
export function dayOfYear(d: Date): number {
  const start = new Date(d.getFullYear(), 0, 0);
  const diff = d.getTime() - start.getTime();
  return Math.floor(diff / 86_400_000);
}
