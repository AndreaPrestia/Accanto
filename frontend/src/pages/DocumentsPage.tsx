import { FormEvent, useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { api, extractError } from '../api/client';
import { DocumentCategory, DocumentCategoryLabel, DocumentItem } from '../types';

const CATS: DocumentCategory[] = ['Report','BloodTest','Imaging','Prescription','Therapy','IdentityDocument','Delegation','HospitalContact','Other'];

export default function DocumentsPage() {
  const { id } = useParams<{ id: string }>();
  const [docs, setDocs] = useState<DocumentItem[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [showForm, setShowForm] = useState(false);

  const load = async () => {
    if (!id) return;
    try {
      const { data } = await api.get<DocumentItem[]>(`/care-circles/${id}/documents`);
      setDocs(data);
    } catch (e) { setError(extractError(e)); }
  };

  useEffect(() => { load(); /* eslint-disable-next-line react-hooks/exhaustive-deps */ }, [id]);

  const download = async (d: DocumentItem) => {
    const res = await api.get(`/care-circles/${id}/documents/${d.id}/download`, { responseType: 'blob' });
    const url = URL.createObjectURL(res.data);
    const a = document.createElement('a');
    a.href = url; a.download = d.originalFileName;
    document.body.appendChild(a); a.click(); a.remove();
    URL.revokeObjectURL(url);
  };

  const del = async (d: DocumentItem) => {
    if (!confirm(`Eliminare "${d.originalFileName}"?`)) return;
    await api.delete(`/care-circles/${id}/documents/${d.id}`);
    load();
  };

  const formatSize = (n: number) => n < 1024 ? `${n} B` : n < 1024*1024 ? `${(n/1024).toFixed(0)} KB` : `${(n/1024/1024).toFixed(1)} MB`;

  return (
    <div>
      <div className="flex items-center justify-between mb-2">
        <h1 className="text-2xl font-semibold">Documenti</h1>
        <Link to={`/care-circles/${id}`} className="text-sm text-accanto-500 hover:underline">← Cerchio</Link>
      </div>
      <p className="text-accanto-500 mb-4">Tieni vicino ciò che ti serve quando ti chiedono un documento.</p>

      <button onClick={() => setShowForm(s => !s)} className="btn-primary mb-4">
        {showForm ? 'Annulla' : '+ Carica documento'}
      </button>

      {showForm && <UploadForm careCircleId={id!} onUploaded={() => { setShowForm(false); load(); }} />}

      {error && <div className="text-sm text-red-700 bg-red-50 border border-red-200 rounded-md px-3 py-2 mb-3">{error}</div>}

      {docs === null ? <p className="text-accanto-500">Caricamento…</p> :
        docs.length === 0 ? <p className="text-accanto-500">Ancora nessun documento.</p> :
        <div className="space-y-3">
          {docs.map(d => (
            <div key={d.id} className="card">
              <div className="flex items-baseline justify-between gap-2">
                <div className="min-w-0">
                  <h3 className="font-medium truncate">{d.originalFileName}</h3>
                  <p className="text-xs text-accanto-500">{DocumentCategoryLabel[d.category]} • {formatSize(d.sizeInBytes)} • {new Date(d.createdAt).toLocaleDateString('it-IT')}</p>
                </div>
              </div>
              {d.notes && <p className="text-sm mt-2">{d.notes}</p>}
              {d.tags.length > 0 && (
                <div className="mt-2 flex flex-wrap gap-1">
                  {d.tags.map(t => <span key={t} className="text-xs bg-accanto-100 text-accanto-700 rounded px-2 py-0.5">{t}</span>)}
                </div>
              )}
              <div className="mt-3 flex gap-2">
                <button onClick={() => download(d)} className="btn-ghost">Scarica</button>
                <button onClick={() => del(d)} className="text-sm text-accanto-500 hover:text-red-700">Elimina</button>
              </div>
            </div>
          ))}
        </div>
      }
    </div>
  );
}

function UploadForm({ careCircleId, onUploaded }: { careCircleId: string; onUploaded: () => void }) {
  const [file, setFile] = useState<File | null>(null);
  const [category, setCategory] = useState<DocumentCategory>('Report');
  const [notes, setNotes] = useState('');
  const [tags, setTags] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (!file) { setError('Scegli un file.'); return; }
    setBusy(true); setError(null);
    try {
      const fd = new FormData();
      fd.append('file', file);
      fd.append('category', category);
      if (notes) fd.append('notes', notes);
      if (tags) fd.append('tags', tags);
      await api.post(`/care-circles/${careCircleId}/documents`, fd, {
        headers: { 'Content-Type': 'multipart/form-data' }
      });
      onUploaded();
    } catch (err) { setError(extractError(err)); }
    finally { setBusy(false); }
  };

  return (
    <form onSubmit={submit} className="card mb-4 space-y-3">
      <div>
        <label className="label">File</label>
        <input className="input" type="file" required onChange={(e) => setFile(e.target.files?.[0] ?? null)} />
        <p className="text-xs text-accanto-500 mt-1">Massimo 20 MB.</p>
      </div>
      <div>
        <label className="label">Categoria</label>
        <select className="input" value={category} onChange={(e) => setCategory(e.target.value as DocumentCategory)}>
          {CATS.map(c => <option key={c} value={c}>{DocumentCategoryLabel[c]}</option>)}
        </select>
      </div>
      <div>
        <label className="label">Note (facoltative)</label>
        <textarea className="input min-h-[60px]" value={notes} onChange={(e) => setNotes(e.target.value)} />
      </div>
      <div>
        <label className="label">Tag (separati da virgola)</label>
        <input className="input" value={tags} onChange={(e) => setTags(e.target.value)} />
      </div>
      {error && <div className="text-sm text-red-700">{error}</div>}
      <button className="btn-primary" disabled={busy}>{busy ? 'Caricamento…' : 'Carica'}</button>
    </form>
  );
}
