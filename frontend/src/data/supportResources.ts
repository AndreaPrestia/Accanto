// Risorse di supporto per chi cura. Contenuto curato manualmente.
// I numeri/URL si riferiscono a organizzazioni italiane stabili e pubbliche;
// in caso di dubbio l'utente trova sempre il link al sito ufficiale.

export type SupportCategory =
  | 'emergency'
  | 'listening'
  | 'dementia'
  | 'palliative'
  | 'caregiver'
  | 'social';

export interface SupportResource {
  id: string;
  category: SupportCategory;
  name: string;
  description: string;
  phone?: string;
  phoneLabel?: string;
  url?: string;
  hours?: string;
}

export const SUPPORT_CATEGORIES: SupportCategory[] = [
  'emergency',
  'listening',
  'dementia',
  'palliative',
  'caregiver',
  'social'
];

export const SUPPORT_RESOURCES: SupportResource[] = [
  {
    id: 'emergency-112',
    category: 'emergency',
    name: 'Numero unico emergenze · 112',
    description:
      'Numero unico europeo per emergenze sanitarie, polizia e vigili del fuoco. Chiama se c\u2019è pericolo immediato per te o per chi assisti.',
    phone: '112',
    phoneLabel: '112',
    hours: '24/7'
  },
  {
    id: 'telefono-amico',
    category: 'listening',
    name: 'Telefono Amico Italia',
    description:
      'Ascolto telefonico anonimo e gratuito quando hai bisogno di parlare con qualcuno. Volontari preparati, nessun giudizio.',
    phone: '+390223272327',
    phoneLabel: '02 2327 2327',
    url: 'https://www.telefonoamico.it',
    hours: 'Tutti i giorni 10:00 \u2013 24:00'
  },
  {
    id: 'samaritans',
    category: 'listening',
    name: 'Samaritans Onlus',
    description:
      'Ascolto telefonico per chi sta attraversando un momento di disperazione profonda o pensieri suicidi. Riservato, anonimo, gratuito.',
    phone: '+390606008686',
    phoneLabel: '06 77208977',
    url: 'https://www.samaritansonlus.org',
    hours: 'Tutti i giorni 13:00 \u2013 22:00'
  },
  {
    id: 'alzheimer-italia',
    category: 'dementia',
    name: 'Federazione Alzheimer Italia',
    description:
      'Informazioni, orientamento e ascolto per familiari di persone con demenza. Punti di contatto in tutta Italia.',
    url: 'https://www.alzheimer.it'
  },
  {
    id: 'telefono-verde-alzheimer',
    category: 'dementia',
    name: 'Telefono Verde Alzheimer (ISS)',
    description:
      'Linea telefonica dell\u2019Istituto Superiore di Sanit\u00e0 dedicata alle demenze: informazioni, indirizzi, supporto.',
    url: 'https://www.iss.it/demenze-osservatorio-demenze'
  },
  {
    id: 'aima',
    category: 'dementia',
    name: 'AIMA \u2013 Associazione Italiana Malattia di Alzheimer',
    description:
      'Rete di sezioni territoriali con gruppi di sostegno per familiari, formazione e attivit\u00e0 di socializzazione per le persone con demenza.',
    url: 'https://www.alzheimer-aima.it'
  },
  {
    id: 'aisla',
    category: 'caregiver',
    name: 'AISLA \u2013 Sclerosi Laterale Amiotrofica',
    description:
      'Sostegno a persone con SLA e ai loro familiari: orientamento, ausili, supporto psicologico, gruppi locali.',
    url: 'https://www.aisla.it'
  },
  {
    id: 'aism',
    category: 'caregiver',
    name: 'AISM \u2013 Sclerosi Multipla',
    description:
      'Servizi e accompagnamento per persone con sclerosi multipla e per chi se ne prende cura. Numero verde nazionale dal sito.',
    url: 'https://www.aism.it'
  },
  {
    id: 'fedcp',
    category: 'palliative',
    name: 'Federazione Cure Palliative',
    description:
      'Cure palliative in Italia: cosa sono, come accedervi, mappa delle realt\u00e0 territoriali. Utile quando il percorso di cura entra in una fase avanzata.',
    url: 'https://www.fedcp.org'
  },
  {
    id: 'ant',
    category: 'palliative',
    name: 'Fondazione ANT \u2013 Assistenza domiciliare gratuita',
    description:
      'Assistenza socio-sanitaria gratuita a domicilio per persone con tumore in fase avanzata. Presente in molte regioni italiane.',
    url: 'https://ant.it'
  },
  {
    id: 'caritas',
    category: 'social',
    name: 'Caritas Italiana',
    description:
      'Per bisogni materiali (cibo, bollette, ascolto) la Caritas diocesana del tuo territorio \u00e8 un primo punto di contatto.',
    url: 'https://www.caritas.it'
  }
];
