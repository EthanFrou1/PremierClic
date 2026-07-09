import React, { useEffect, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import { googleBusinessUrl } from '../utils/google'
import { STATUS_ORDER, statusMeta } from '../utils/status'
import { loadGoogleMapsScript } from '../utils/loadGoogleMaps'

export default function Prospects(){
  const [list, setList] = useState([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState(null)
  const [filters, setFilters] = useState({ statut: '', categorie: '', ville: '' })
  const [importing, setImporting] = useState(false)
  const [resultModal, setResultModal] = useState(null)
  const [refreshingId, setRefreshingId] = useState(null)
  const [googleConfigOpen, setGoogleConfigOpen] = useState(false)
  const [googleConfig, setGoogleConfig] = useState({ maxResults: 20, radiusMeters: 5000, ville: '' })
  const villeInputRef = useRef(null)
  const autocompleteRef = useRef(null)

  useEffect(() => {
    if (!googleConfigOpen) return
    let cancelled = false

    loadGoogleMapsScript().then(google => {
      if (cancelled || !villeInputRef.current) return
      autocompleteRef.current = new google.maps.places.Autocomplete(villeInputRef.current, {
        types: ['(cities)'],
        componentRestrictions: { country: 'fr' }
      })
      autocompleteRef.current.addListener('place_changed', () => {
        const place = autocompleteRef.current.getPlace()
        const description = place?.formatted_address || place?.name
        if (description) {
          setGoogleConfig(prev => ({ ...prev, ville: description }))
        }
      })
    }).catch(err => {
      setError(err.message)
    })

    return () => {
      cancelled = true
      if (autocompleteRef.current) {
        window.google?.maps?.event?.clearInstanceListeners(autocompleteRef.current)
        autocompleteRef.current = null
      }
    }
  }, [googleConfigOpen])

  useEffect(() => { fetchList() }, [])

  async function fetchList(){
    setLoading(true); setError(null)
    try{
      const params = new URLSearchParams()
      if (filters.statut) params.set('statut', filters.statut)
      if (filters.categorie) params.set('categorie', filters.categorie)
      if (filters.ville) params.set('ville', filters.ville)
      const res = await fetch((import.meta.env.VITE_API_BASE_URL||'') + '/api/prospects?' + params.toString())
      if (!res.ok) throw new Error('Échec du chargement des prospects')
      const data = await res.json()
      setList(data)
    }catch(err){ setError(err.message) }
    finally{ setLoading(false) }
  }

  function handleFilterChange(e){ setFilters({ ...filters, [e.target.name]: e.target.value }) }

  async function handleDiscovery() {
    setImporting(true)

    try {
      const res = await fetch((import.meta.env.VITE_API_BASE_URL || '') + '/api/prospects/discover', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ latitude: 42.688, longitude: 2.894, radiusMeters: 5000, maxResults: 100, categories: ["shop", "amenity"] })
      })
      if (!res.ok) {
        const text = await res.text()
        throw new Error(text || 'Erreur découverte')
      }
      const payload = await res.json()
      await fetchList()
      setResultModal({ source: 'Overpass', imported: payload.imported, prospects: payload.prospects || [], error: null })
    } catch (err) {
      setResultModal({ source: 'Overpass', imported: 0, prospects: [], error: err.message || 'Erreur inconnue' })
    } finally {
      setImporting(false)
    }
  }

  async function handleGoogleDiscovery(config) {
    setGoogleConfigOpen(false)
    setImporting(true)

    try {
      const res = await fetch((import.meta.env.VITE_API_BASE_URL || '') + '/api/prospects/discover/google', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ latitude: 42.688, longitude: 2.894, radiusMeters: config.radiusMeters, maxResults: config.maxResults, ville: config.ville || null, categories: ["store"] })
      })
      if (!res.ok) {
        const text = await res.text()
        throw new Error(text || 'Erreur découverte Google')
      }
      const payload = await res.json()
      await fetchList()
      setResultModal({ source: 'Google Places', imported: payload.imported, prospects: payload.prospects || [], error: null })
    } catch (err) {
      setResultModal({ source: 'Google Places', imported: 0, prospects: [], error: err.message || 'Erreur inconnue' })
    } finally {
      setImporting(false)
    }
  }

  async function handleRefreshExisting() {
    setImporting(true)

    try {
      const res = await fetch((import.meta.env.VITE_API_BASE_URL || '') + '/api/prospects/discover/google/refresh-existing?maxResults=25', {
        method: 'POST'
      })
      if (!res.ok) {
        const text = await res.text()
        throw new Error(text || 'Erreur de rafraîchissement')
      }
      const payload = await res.json()
      await fetchList()
      setResultModal({ mode: 'refresh', source: 'Google Places', checkedCount: payload.checkedCount, updated: payload.updated, notFound: payload.notFound, prospects: payload.prospects || [], error: null })
    } catch (err) {
      setResultModal({ mode: 'refresh', source: 'Google Places', error: err.message || 'Erreur inconnue' })
    } finally {
      setImporting(false)
    }
  }

  async function handleRefreshOne(id) {
    setRefreshingId(id)
    try {
      const res = await fetch((import.meta.env.VITE_API_BASE_URL || '') + `/api/prospects/discover/google/refresh/${id}`, {
        method: 'POST'
      })
      if (!res.ok) {
        const text = await res.text()
        throw new Error(text || 'Erreur de rafraîchissement')
      }
      const payload = await res.json()
      if (payload.found && payload.prospect) {
        setList(list.map(p => p.id === id ? payload.prospect : p))
      }
    } catch (err) {
      setError(err.message || 'Erreur inconnue')
    } finally {
      setRefreshingId(null)
    }
  }

  return (
    <div className="p-6">
      <div className="mb-6 flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h2 className="text-2xl font-semibold">Prospects</h2>
          <p className="text-sm text-slate-500">Importer des commerces locaux sans site et visualiser les résultats.</p>
        </div>
        <Link to="/" className="rounded-md bg-slate-900 px-4 py-2 text-sm font-semibold text-white hover:bg-slate-700">Accueil</Link>
      </div>

      <div className="mb-6 rounded-3xl bg-white p-6 shadow-sm shadow-slate-200">
        <h3 className="text-lg font-semibold">Importer des prospects</h3>
        <p className="text-sm text-slate-500">Lance la découverte automatique et ajoute des commerces locaux sans saisie manuelle.</p>
        <div className="mt-4 flex flex-wrap items-center gap-3">
          <button onClick={handleDiscovery} disabled={importing} className="rounded-xl bg-emerald-600 px-5 py-3 text-sm font-semibold text-white hover:bg-emerald-500 disabled:opacity-50">
            {importing ? 'Import en cours...' : 'Découvrir via Overpass'}
          </button>
          <button onClick={() => setGoogleConfigOpen(true)} disabled={importing} className="rounded-xl bg-sky-600 px-5 py-3 text-sm font-semibold text-white hover:bg-sky-500 disabled:opacity-50">
            {importing ? 'Import en cours...' : 'Découvrir via Google Places'}
          </button>
          <button onClick={handleRefreshExisting} disabled={importing} className="rounded-xl bg-amber-600 px-5 py-3 text-sm font-semibold text-white hover:bg-amber-500 disabled:opacity-50">
            {importing ? 'Import en cours...' : 'Compléter les commerces existants'}
          </button>
        </div>
      </div>

      <div className="mb-4 flex flex-col gap-3 sm:flex-row">
        <input name="statut" placeholder="Statut" value={filters.statut} onChange={handleFilterChange} className="rounded-xl border border-slate-200 bg-slate-50 px-3 py-2" />
        <input name="categorie" placeholder="Catégorie" value={filters.categorie} onChange={handleFilterChange} className="rounded-xl border border-slate-200 bg-slate-50 px-3 py-2" />
        <input name="ville" placeholder="Ville" value={filters.ville} onChange={handleFilterChange} className="rounded-xl border border-slate-200 bg-slate-50 px-3 py-2" />
        <button onClick={fetchList} className="rounded-xl bg-slate-900 px-5 py-2 text-sm font-semibold text-white hover:bg-slate-700">Filtrer</button>
      </div>

      {loading && <div className="mb-4 rounded-3xl bg-slate-50 p-4 text-sm text-slate-600 shadow-sm shadow-slate-200">Chargement des prospects...</div>}
      {error && <div className="mb-4 rounded-3xl bg-rose-50 p-4 text-sm text-rose-700 shadow-sm shadow-rose-100">{error}</div>}

      <div className="overflow-x-auto rounded-3xl border border-slate-200 bg-white shadow-sm shadow-slate-200">
        <table className="min-w-full border-collapse">
          <thead>
            <tr className="bg-slate-100 text-left text-sm uppercase tracking-[0.08em] text-slate-600">
              <th className="px-4 py-3">Nom</th>
              <th className="px-4 py-3">Statut</th>
              <th className="px-4 py-3">Ville</th>
              <th className="px-4 py-3">Email</th>
              <th className="px-4 py-3">Actions</th>
            </tr>
          </thead>
          <tbody>
            {list.map(p => {
              const meta = statusMeta(p.statut)
              return (
                <tr key={p.id} className={`border-t border-slate-200 ${meta.row}`}>
                  <td className="px-4 py-3">
                    <a href={googleBusinessUrl(p)} target="_blank" rel="noreferrer" className="text-sky-700 hover:underline">
                      {p.nom}
                    </a>
                  </td>
                  <td className="px-4 py-3">
                    <span className={`inline-flex items-center gap-2 text-sm font-medium ${meta.text}`}>
                      <span className={`h-2.5 w-2.5 rounded-full ${meta.dot}`}></span>
                      {meta.label}
                    </span>
                  </td>
                  <td className="px-4 py-3">{p.ville}</td>
                  <td className="px-4 py-3">{p.email}</td>
                  <td className="px-4 py-3">
                    <div className="flex items-center gap-2">
                      <Link to={`/prospects/${p.id}`} className="rounded-full bg-slate-900 px-3 py-1 text-xs font-semibold text-white hover:bg-slate-700">Voir</Link>
                      <button
                        onClick={() => handleRefreshOne(p.id)}
                        disabled={refreshingId === p.id}
                        className="rounded-full bg-amber-100 px-3 py-1 text-xs font-semibold text-amber-800 hover:bg-amber-200 disabled:opacity-50"
                      >
                        {refreshingId === p.id ? '...' : 'Rafraîchir'}
                      </button>
                    </div>
                  </td>
                </tr>
              )
            })}
            {list.length === 0 && !loading && (
              <tr>
                <td colSpan="5" className="px-4 py-8 text-center text-sm text-slate-500">Aucun prospect trouvé. Lance une importation pour commencer.</td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      <div className="mt-4 flex flex-wrap items-center gap-x-5 gap-y-2 rounded-3xl bg-white px-5 py-4 text-sm shadow-sm shadow-slate-200">
        <span className="font-semibold text-slate-700">Légende :</span>
        {STATUS_ORDER.map(statut => {
          const meta = statusMeta(statut)
          return (
            <span key={statut} className="inline-flex items-center gap-2 text-slate-600">
              <span className={`h-2.5 w-2.5 rounded-full ${meta.dot}`}></span>
              {meta.label}
            </span>
          )
        })}
      </div>

      {googleConfigOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/40 p-4">
          <div className="w-full max-w-sm rounded-3xl bg-white p-8 shadow-xl shadow-slate-900/10">
            <h3 className="text-lg font-semibold">Paramètres de l'import Google Places</h3>
            <p className="mt-2 text-sm text-slate-500">Ajuste la zone et le nombre de résultats avant de lancer la recherche.</p>
            <div className="mt-4 space-y-3">
              <label className="block space-y-1 text-sm">
                <span className="font-medium text-slate-700">Nombre max de résultats</span>
                <input
                  type="number"
                  min="1"
                  max="50"
                  value={googleConfig.maxResults}
                  onChange={e => setGoogleConfig({ ...googleConfig, maxResults: Number(e.target.value) })}
                  className="w-full rounded-xl border border-slate-200 bg-slate-50 px-3 py-2"
                />
              </label>
              <label className="block space-y-1 text-sm">
                <span className="font-medium text-slate-700">Ville</span>
                <input
                  ref={villeInputRef}
                  type="text"
                  placeholder="ex. Perpignan"
                  autoComplete="off"
                  defaultValue={googleConfig.ville}
                  onChange={e => setGoogleConfig({ ...googleConfig, ville: e.target.value })}
                  className="w-full rounded-xl border border-slate-200 bg-slate-50 px-3 py-2"
                />
              </label>
              <label className={`block space-y-1 text-sm ${googleConfig.ville ? 'opacity-40' : ''}`}>
                <span className="font-medium text-slate-700">Rayon de recherche (mètres)</span>
                <input
                  type="number"
                  min="100"
                  max="50000"
                  step="100"
                  disabled={!!googleConfig.ville}
                  value={googleConfig.radiusMeters}
                  onChange={e => setGoogleConfig({ ...googleConfig, radiusMeters: Number(e.target.value) })}
                  className="w-full rounded-xl border border-slate-200 bg-slate-50 px-3 py-2 disabled:cursor-not-allowed"
                />
                <span className="block text-xs text-slate-500">Ignoré si une ville est renseignée.</span>
              </label>
            </div>
            <div className="mt-6 flex gap-3">
              <button
                onClick={() => setGoogleConfigOpen(false)}
                className="flex-1 rounded-xl border border-slate-200 px-4 py-2 text-sm font-semibold text-slate-700 hover:bg-slate-50"
              >
                Annuler
              </button>
              <button
                onClick={() => handleGoogleDiscovery(googleConfig)}
                className="flex-1 rounded-xl bg-sky-600 px-4 py-2 text-sm font-semibold text-white hover:bg-sky-500"
              >
                Lancer l'import
              </button>
            </div>
          </div>
        </div>
      )}

      {importing && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/40 p-4">
          <div className="rounded-3xl bg-white p-8 text-center shadow-xl shadow-slate-900/10">
            <div className="mb-4 flex items-center justify-center">
              <div className="h-12 w-12 animate-spin rounded-full border-4 border-slate-300 border-t-slate-900"></div>
            </div>
            <h3 className="text-lg font-semibold">Import en cours</h3>
            <p className="mt-2 text-sm text-slate-500">Patiente pendant que les prospects sont récupérés et ajoutés à la base.</p>
          </div>
        </div>
      )}

      {resultModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/40 p-4">
          <div className="w-full max-w-md rounded-3xl bg-white p-8 shadow-xl shadow-slate-900/10">
            {resultModal.error ? (
              <>
                <h3 className="text-lg font-semibold text-rose-700">
                  {resultModal.mode === 'refresh' ? 'Échec du rafraîchissement' : "Échec de l'import"} — {resultModal.source}
                </h3>
                <p className="mt-2 text-sm text-slate-600">{resultModal.error}</p>
              </>
            ) : resultModal.mode === 'refresh' ? (
              <>
                <h3 className="text-lg font-semibold">Rafraîchissement terminé — {resultModal.source}</h3>
                <p className="mt-2 text-sm text-slate-600">
                  {resultModal.updated} commerce{resultModal.updated > 1 ? 's' : ''} complété{resultModal.updated > 1 ? 's' : ''} sur {resultModal.checkedCount} vérifié{resultModal.checkedCount > 1 ? 's' : ''}
                  {resultModal.notFound > 0 ? ` (${resultModal.notFound} introuvable${resultModal.notFound > 1 ? 's' : ''} sur Google).` : '.'}
                </p>
                {resultModal.prospects.length > 0 && (
                  <ul className="mt-4 max-h-64 space-y-2 overflow-y-auto text-sm text-slate-700">
                    {resultModal.prospects.map(p => (
                      <li key={p.id} className="rounded-xl bg-amber-50 px-3 py-2">
                        <span className="font-medium">{p.nom}</span>
                        {p.ville && <span className="text-slate-500"> — {p.ville}</span>}
                        {p.telephone && <span className="text-slate-500"> · {p.telephone}</span>}
                      </li>
                    ))}
                  </ul>
                )}
              </>
            ) : (
              <>
                <h3 className="text-lg font-semibold">Import terminé — {resultModal.source}</h3>
                <p className="mt-2 text-sm text-slate-600">
                  {resultModal.imported > 0
                    ? `${resultModal.imported} commerce${resultModal.imported > 1 ? 's' : ''} importé${resultModal.imported > 1 ? 's' : ''}.`
                    : "Aucun nouveau commerce trouvé pour cette zone."}
                </p>
                {resultModal.prospects.length > 0 && (
                  <ul className="mt-4 max-h-64 space-y-2 overflow-y-auto text-sm text-slate-700">
                    {resultModal.prospects.map(p => (
                      <li key={p.id} className="rounded-xl bg-slate-50 px-3 py-2">
                        <span className="font-medium">{p.nom}</span>
                        {p.ville && <span className="text-slate-500"> — {p.ville}</span>}
                      </li>
                    ))}
                  </ul>
                )}
              </>
            )}
            <button
              onClick={() => setResultModal(null)}
              className="mt-6 w-full rounded-xl bg-slate-900 px-4 py-2 text-sm font-semibold text-white hover:bg-slate-700"
            >
              Fermer
            </button>
          </div>
        </div>
      )}
    </div>
  )
}
