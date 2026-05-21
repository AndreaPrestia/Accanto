import { FormEvent, useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { api, extractError } from '../api/client';
import { DoctorQuestion, DoctorQuestionCategory, DoctorQuestionStatus, DoctorQuestionTemplate, QuestionCategoryLabel, QuestionStatusLabel } from '../types';

const CATS: DoctorQuestionCategory[] = ['Diagnosis','Therapy','Pain','Nutrition','Hydration','PalliativeCare','Discharge','HomeCare','Emergency','Prognosis','Practical','Other'];
const STATUSES: DoctorQuestionStatus[] = ['ToAsk','Asked','Answered','Archived'];

export default function DoctorQuestionsPage() {
  const { id } = useParams<{ id: string }>();
  const [items, setItems] = useState<DoctorQuestion[] | null>(null);
  const [templates, setTemplates] = useState<DoctorQuestionTemplate[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [showForm, setShowForm] = useState(false);
  const [prefill, setPrefill] = useState<{ q: string; c: DoctorQuestionCategory } | null>(null);

  const load = async () => {
    if (!id) return;
    try {
      const { data } = await api.get<DoctorQuestion[]>(`/care-circles/${id}/doctor-questions`);
      setItems(data);
    } catch (e) { setError(extractError(e)); }
  };

  useEffect(() => { load(); /* eslint-disable-next-line */ }, [id]);
  useEffect(() => {
    api.get<DoctorQuestionTemplate[]>('/doctor-question-templates').then(r => setTemplates(r.data)).catch(() => {});
  }, []);

  const updateStatus = async (q: DoctorQuestion, status: DoctorQuestionStatus) => {
    await api.put(`/care-circles/${id}/doctor-questions/${q.id}`, {
      question: q.question, category: q.category, status, answerNotes: q.answerNotes ?? null
    });
    load();
  };

  const del = async (q: DoctorQuestion) => {
    if (!confirm('Eliminare questa domanda?')) return;
    await api.delete(`/care-circles/${id}/doctor-questions/${q.id}`);
    load();
  };

  return (
    <div>
      <div className="flex items-center justify-between mb-2">
        <h1 className="text-2xl font-semibold">Domande per il medico</h1>
        <Link to={`/care-circles/${id}`} className="text-sm text-accanto-500 hover:underline">← Cerchio</Link>
      </div>
      <p className="text-accanto-500 mb-4">Annota le domande quando ti vengono in mente. Riprenderle prima della visita aiuta.</p>

      <button onClick={() => { setPrefill(null); setShowForm(s => !s); }} className="btn-primary mb-4">
        {showForm ? 'Annulla' : '+ Nuova domanda'}
      </button>

      {showForm && <NewForm careCircleId={id!} prefill={prefill} onCreated={() => { setShowForm(false); setPrefill(null); load(); }} />}

      {templates.length > 0 && (
        <details className="card mb-4">
          <summary className="cursor-pointer font-medium">Suggerimenti per categoria</summary>
          <div className="mt-3 space-y-3">
            {templates.map(t => (
              <div key={t.category}>
                <p className="text-sm font-medium">{t.categoryLabel}</p>
                <ul className="mt-1 space-y-1">
                  {t.questions.map(q => (
                    <li key={q} className="text-sm flex items-start gap-2">
                      <button
                        type="button"
                        className="text-accanto-700 hover:underline text-left"
                        onClick={() => { setPrefill({ q, c: t.category }); setShowForm(true); }}
                      >+ {q}</button>
                    </li>
                  ))}
                </ul>
              </div>
            ))}
          </div>
        </details>
      )}

      {error && <div className="text-sm text-red-700 bg-red-50 border border-red-200 rounded-md px-3 py-2 mb-3">{error}</div>}

      {items === null ? <p className="text-accanto-500">Caricamento…</p> :
        items.length === 0 ? <p className="text-accanto-500">Ancora nessuna domanda.</p> :
        <div className="space-y-3">
          {items.map(q => (
            <div key={q.id} className="card">
              <p className="font-medium">{q.question}</p>
              <p className="text-xs text-accanto-500 mt-1">{QuestionCategoryLabel[q.category]} • {QuestionStatusLabel[q.status]}</p>
              {q.answerNotes && <p className="text-sm mt-2 whitespace-pre-wrap"><span className="text-accanto-500">Risposta: </span>{q.answerNotes}</p>}
              <div className="mt-3 flex flex-wrap gap-2 items-center">
                <select className="input max-w-[200px]" value={q.status} onChange={(e) => updateStatus(q, e.target.value as DoctorQuestionStatus)}>
                  {STATUSES.map(s => <option key={s} value={s}>{QuestionStatusLabel[s]}</option>)}
                </select>
                <button onClick={() => del(q)} className="text-sm text-accanto-500 hover:text-red-700">Elimina</button>
              </div>
            </div>
          ))}
        </div>
      }
    </div>
  );
}

function NewForm({ careCircleId, prefill, onCreated }: { careCircleId: string; prefill: { q: string; c: DoctorQuestionCategory } | null; onCreated: () => void }) {
  const [question, setQuestion] = useState(prefill?.q ?? '');
  const [category, setCategory] = useState<DoctorQuestionCategory>(prefill?.c ?? 'Other');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true); setError(null);
    try {
      await api.post(`/care-circles/${careCircleId}/doctor-questions`, { question, category });
      onCreated();
    } catch (err) { setError(extractError(err)); }
    finally { setBusy(false); }
  };

  return (
    <form onSubmit={submit} className="card mb-4 space-y-3">
      <div>
        <label className="label">Domanda</label>
        <textarea className="input min-h-[80px]" required value={question} onChange={(e) => setQuestion(e.target.value)} />
      </div>
      <div>
        <label className="label">Categoria</label>
        <select className="input" value={category} onChange={(e) => setCategory(e.target.value as DoctorQuestionCategory)}>
          {CATS.map(c => <option key={c} value={c}>{QuestionCategoryLabel[c]}</option>)}
        </select>
      </div>
      {error && <div className="text-sm text-red-700">{error}</div>}
      <button className="btn-primary" disabled={busy}>{busy ? 'Salvataggio…' : 'Aggiungi domanda'}</button>
    </form>
  );
}
