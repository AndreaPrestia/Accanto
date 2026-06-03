// Sorgente unica delle FAQ trilingua.
// Usata sia per il rendering delle pagine FAQ sia per emettere
// il JSON-LD FAQPage (rich result Google).
import type { Locale } from '../i18n';

export interface FaqEntry {
  question: string;
  answer: string;
}

const it: FaqEntry[] = [
  {
    question: 'Accanto è un dispositivo medico?',
    answer: 'No. Accanto è uno strumento di supporto organizzativo. Non sostituisce il medico curante né un consulto sanitario qualificato.'
  },
  {
    question: 'Quanto costa?',
    answer: 'Durante la fase beta è gratis. Non abbiamo ancora definito i piani futuri: l\'obiettivo è restare accessibile per le famiglie.'
  },
  {
    question: 'Chi può vedere i miei dati?',
    answer: 'Solo le persone che inviti nel cerchio di cura, secondo i ruoli che assegni. Nessun amministratore di Accanto accede ai contenuti che inserisci.'
  },
  {
    question: 'Posso installare Accanto sul mio server?',
    answer: 'Sì. Accanto è pensato anche per essere self-hosted, così i dati restano fisicamente nel tuo ambiente.'
  },
  {
    question: 'L\'intelligenza artificiale è obbligatoria?',
    answer: 'No. È opzionale e spenta di default. Quando attiva, può essere collegata a un modello locale per non far uscire i dati dal tuo ambiente.'
  },
  {
    question: 'Come esporto i miei dati?',
    answer: 'Dalla tua area personale puoi richiedere l\'esportazione completa dei dati in qualunque momento.'
  },
  {
    question: 'In quali lingue è disponibile?',
    answer: 'Italiano, inglese e spagnolo. Altre lingue arriveranno in base alle richieste.'
  }
];

const en: FaqEntry[] = [
  {
    question: 'Is Accanto a medical device?',
    answer: 'No. Accanto is an organizational support tool. It does not replace your doctor or any qualified medical advice.'
  },
  {
    question: 'How much does it cost?',
    answer: 'During the beta phase it is free. We have not defined future plans yet — our goal is to stay accessible to families.'
  },
  {
    question: 'Who can see my data?',
    answer: 'Only the people you invite into the care circle, according to the roles you assign. No Accanto administrator accesses the content you enter.'
  },
  {
    question: 'Can I install Accanto on my own server?',
    answer: 'Yes. Accanto is designed to be self-hosted, so data stays physically in your environment.'
  },
  {
    question: 'Is the AI mandatory?',
    answer: 'No. It is optional and off by default. When on, it can be connected to a local model to keep data inside your environment.'
  },
  {
    question: 'How do I export my data?',
    answer: 'You can request a full data export from your personal area at any time.'
  },
  {
    question: 'Which languages are supported?',
    answer: 'Italian, English and Spanish. More languages will follow based on requests.'
  }
];

const es: FaqEntry[] = [
  {
    question: '¿Es Accanto un dispositivo médico?',
    answer: 'No. Accanto es una herramienta de apoyo organizativo. No sustituye al médico de cabecera ni a un consejo médico cualificado.'
  },
  {
    question: '¿Cuánto cuesta?',
    answer: 'Durante la fase beta es gratis. Aún no hemos definido los planes futuros: el objetivo es seguir siendo accesible para las familias.'
  },
  {
    question: '¿Quién puede ver mis datos?',
    answer: 'Solo las personas que invitas al círculo de cuidado, según los roles que asignes. Ningún administrador de Accanto accede a los contenidos que introduces.'
  },
  {
    question: '¿Puedo instalar Accanto en mi servidor?',
    answer: 'Sí. Accanto está pensado para poder ser self-hosted, así los datos permanecen físicamente en tu entorno.'
  },
  {
    question: '¿La inteligencia artificial es obligatoria?',
    answer: 'No. Es opcional y está desactivada por defecto. Cuando se activa puede conectarse a un modelo local para que los datos no salgan de tu entorno.'
  },
  {
    question: '¿Cómo exporto mis datos?',
    answer: 'Desde tu área personal puedes solicitar la exportación completa de los datos en cualquier momento.'
  },
  {
    question: '¿En qué idiomas está disponible?',
    answer: 'Italiano, inglés y español. Otros idiomas llegarán según las peticiones.'
  }
];

const dictionaries: Record<Locale, FaqEntry[]> = { it, en, es };

export function getFaq(locale: Locale): FaqEntry[] {
  return dictionaries[locale];
}

/**
 * Costruisce il payload JSON-LD per uno schema FAQPage (schema.org).
 * Restituisce una stringa già serializzata, pronta per `set:html`.
 */
export function faqJsonLd(locale: Locale): string {
  const entries = getFaq(locale);
  return JSON.stringify({
    '@context': 'https://schema.org',
    '@type': 'FAQPage',
    mainEntity: entries.map((e) => ({
      '@type': 'Question',
      name: e.question,
      acceptedAnswer: {
        '@type': 'Answer',
        text: e.answer
      }
    }))
  });
}
