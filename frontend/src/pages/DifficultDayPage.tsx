import { Link, useParams } from 'react-router-dom';

const SUGGESTIONS = [
  'Concediti tre respiri lenti, senza fare altro.',
  'Bevi un bicchiere d\u2019acqua. Mangia qualcosa, anche poco.',
  'Scrivi una sola frase nel diario, anche dura. Non deve essere bella.',
  'Manda un messaggio a una persona di fiducia: basta "ho una giornata difficile".',
  'Se puoi, esci cinque minuti. Anche solo sulla soglia.',
  'Non sei sola. Non sei solo. Stai facendo molto.'
];

export default function DifficultDayPage() {
  const { id } = useParams<{ id: string }>();

  return (
    <div className="max-w-md mx-auto pt-2">
      <div className="flex items-center justify-between mb-2">
        <h1 className="text-2xl font-semibold">Giornata difficile</h1>
        <Link to={`/care-circles/${id}`} className="text-sm text-accanto-500 hover:underline">← Cerchio</Link>
      </div>
      <p className="text-accanto-500 mb-6">
        Quando tutto pesa, prova uno di questi piccoli gesti. Non risolvono, ma fanno respirare.
      </p>

      <ol className="space-y-3 list-decimal list-inside">
        {SUGGESTIONS.map((s, i) => (
          <li key={i} className="card">
            <span>{s}</span>
          </li>
        ))}
      </ol>

      <div className="mt-8 text-sm text-accanto-500">
        <p className="mb-2">Se senti di non farcela, parlare con qualcuno aiuta.</p>
        <p>
          <Link to="/support" className="text-accanto-700 underline">
            Vedi i contatti di supporto →
          </Link>
        </p>
        <p className="mt-1">Se c&rsquo;è un&rsquo;emergenza sanitaria, chiama il 112.</p>
      </div>
    </div>
  );
}
