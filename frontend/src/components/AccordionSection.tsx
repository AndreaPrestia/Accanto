import { ReactNode, useEffect, useRef } from 'react';

interface Props {
  /** Id opzionale sull'`<details>` (utile per anchor cross-page). */
  id?: string;
  /** Titolo visibile nell'header dell'accordion. */
  title: string;
  /** Testo secondario (chip) accanto al titolo, es. "1 suggerimento". */
  hint?: string | null;
  /**
   * Stato iniziale open. Se cambia dopo il mount (es. cambia hash),
   * riapre l'accordion. Non forza la chiusura: rispetta l'interazione utente.
   */
  defaultOpen?: boolean;
  children: ReactNode;
}

/**
 * Accordion basato su `<details>` HTML nativo:
 *  - accessibilità gratuita (spazio/enter aprono/chiudono, screen reader)
 *  - niente JS di gestione stato open (nativo)
 *  - `defaultOpen` sync con l'`open` attribute al primo mount; se successivamente
 *    diventa true (es. deep link), riapriamo esplicitamente. Se torna false,
 *    NON chiudiamo (l'utente potrebbe averlo aperto a mano).
 */
export default function AccordionSection({
  id,
  title,
  hint,
  defaultOpen,
  children
}: Props) {
  const ref = useRef<HTMLDetailsElement>(null);

  useEffect(() => {
    if (defaultOpen && ref.current && !ref.current.open) {
      ref.current.open = true;
    }
  }, [defaultOpen]);

  return (
    <details
      ref={ref}
      id={id}
      open={defaultOpen}
      className="rounded-xl border border-accanto-100 bg-white overflow-hidden group"
    >
      <summary
        className="cursor-pointer list-none px-4 py-3 flex items-center justify-between select-none marker:hidden"
      >
        <span className="flex items-baseline gap-2 min-w-0">
          <span className="text-base font-semibold text-accanto-900 truncate">
            {title}
          </span>
          {hint ? (
            <span className="text-xs text-amber-800 bg-amber-100 rounded-full px-2 py-0.5 whitespace-nowrap">
              {hint}
            </span>
          ) : null}
        </span>
        <span
          aria-hidden="true"
          className="ml-3 text-accanto-500 transition-transform group-open:rotate-180"
        >
          ▾
        </span>
      </summary>
      <div className="px-4 py-4 border-t border-accanto-100 space-y-6">
        {children}
      </div>
    </details>
  );
}
