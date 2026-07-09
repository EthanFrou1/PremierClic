import React, { useState } from 'react'
import { Link } from 'react-router-dom'

export default function Home() {
  const [file, setFile] = useState(null)
  const [loading, setLoading] = useState(false)
  const [message, setMessage] = useState('')

  async function handleUpload() {
    if (!file) {
      setMessage('Choisis un fichier JSON ou CSV avant de lancer l’import.')
      return
    }

    setLoading(true)
    setMessage('')
    const formData = new FormData()
    formData.append('file', file)

    try {
      const res = await fetch((import.meta.env.VITE_API_BASE_URL || '') + '/api/prospects/import', {
        method: 'POST',
        body: formData
      })
      if (!res.ok) {
        const text = await res.text()
        throw new Error(text || 'Import failed')
      }
      const payload = await res.json()
      setMessage(`${payload.imported} prospects importés avec succès.`)
      setFile(null)
    } catch (err) {
      setMessage(`Erreur import : ${err.message}`)
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="min-h-screen bg-slate-50 text-slate-900">
      <header className="border-b border-slate-200 bg-white">
        <div className="mx-auto flex max-w-6xl items-center justify-between px-6 py-6">
          <div>
            <span className="inline-flex items-center gap-2 rounded-full bg-sky-100 px-3 py-1 text-sm font-semibold text-sky-700">
              PremierClic
            </span>
            <p className="mt-2 text-sm text-slate-500">Outil de prospection local pour Perpignan / Pyrénées-Orientales.</p>
          </div>
          <nav className="flex items-center gap-3 text-sm text-slate-600">
            <Link to="/" className="font-medium text-slate-900">Accueil</Link>
            <Link to="/prospects" className="rounded-md bg-slate-900 px-4 py-2 text-white hover:bg-slate-700">Prospects</Link>
          </nav>
        </div>
      </header>

      <main className="mx-auto max-w-6xl px-6 py-16">
        <div className="grid gap-12 lg:grid-cols-[1.3fr_0.9fr] lg:items-center">
          <section className="space-y-6">
            <p className="inline-flex items-center rounded-full bg-sky-100 px-4 py-1 text-sm font-semibold text-sky-700">Prototype interne</p>
            <h1 className="text-4xl font-semibold tracking-tight text-slate-900 sm:text-5xl">
              Suis tes prospects locaux et fais grandir ton activité.
            </h1>
            <p className="max-w-2xl text-base leading-8 text-slate-600">
              PremierClic est conçu pour toi : une mini-CRM légère pour petites entreprises, avec suivi des statuts, notes, maquettes et prospection par email.
            </p>
            <div className="flex flex-col gap-3 sm:flex-row">
              <Link to="/prospects" className="inline-flex items-center justify-center rounded-lg bg-slate-900 px-6 py-3 text-sm font-semibold text-white shadow-sm shadow-slate-200 transition hover:bg-slate-700">
                Accéder aux prospects
              </Link>
              <a href="#features" className="inline-flex items-center justify-center rounded-lg border border-slate-300 px-6 py-3 text-sm font-semibold text-slate-800 transition hover:bg-slate-100">
                Voir les fonctionnalités
              </a>
            </div>
          </section>

          <aside className="grid gap-4">
            <div className="rounded-3xl bg-white p-6 shadow-sm shadow-slate-200">
              <p className="text-sm font-semibold uppercase tracking-[0.3em] text-slate-500">Vue d'ensemble</p>
              <h2 className="mt-4 text-2xl font-semibold text-slate-900">Interface claire</h2>
              <p className="mt-3 text-slate-600">Garde le focus sur les prospects, filtre par statut et vois rapidement ce qui reste à relancer.</p>
            </div>
            <div className="rounded-3xl bg-slate-950 p-6 text-white shadow-sm shadow-slate-200">
              <p className="text-sm uppercase tracking-[0.3em] text-slate-300">En développement</p>
              <h2 className="mt-4 text-2xl font-semibold">Maquettes et emails</h2>
              <p className="mt-3 text-slate-300">Prochainement : ajout de maquettes, upload d'images et génération d'emails avec suivi.</p>
            </div>
          </aside>
        </div>

        <section id="features" className="mt-20">
          <div className="grid gap-6 sm:grid-cols-2 xl:grid-cols-4">
            {[
              { title: 'Suivi des statuts', description: 'ANouveauFait → Contacté → Client', color: 'bg-sky-100', icon: '✔' },
              { title: 'Notes & relances', description: 'Retiens tout ce qui compte pour chaque prospect.', color: 'bg-emerald-100', icon: '📝' },
              { title: 'Maquettes', description: 'Stocke un lien ou une capture d’écran par prospect.', color: 'bg-amber-100', icon: '🖼' },
              { title: 'Email personnalisé', description: 'Prépare des campagnes de prospection rapides.', color: 'bg-rose-100', icon: '✉' }
            ].map(card => (
              <article key={card.title} className="rounded-3xl border border-slate-200 bg-white p-6 shadow-sm">
                <div className={`mb-4 inline-flex h-12 w-12 items-center justify-center rounded-2xl ${card.color} text-2xl`}>
                  {card.icon}
                </div>
                <h3 className="text-lg font-semibold text-slate-900">{card.title}</h3>
                <p className="mt-2 text-slate-600">{card.description}</p>
              </article>
            ))}
          </div>
        </section>

        <section className="mt-16 rounded-3xl bg-white p-6 shadow-sm shadow-slate-200">
          <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
            <div>
              <p className="text-sm font-semibold uppercase tracking-[0.3em] text-slate-500">Importer des prospects</p>
              <h2 className="mt-3 text-2xl font-semibold text-slate-900">JSON ou CSV</h2>
              <p className="mt-2 max-w-2xl text-slate-600">Importe un fichier de prospects généré depuis Overpass ou un export manuel.</p>
            </div>
            <div className="flex items-center gap-3">
              <label className="cursor-pointer rounded-md border border-slate-300 bg-slate-100 px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-200">
                Choisir un fichier
                <input type="file" accept=".json,.csv" className="hidden" onChange={e => setFile(e.target.files?.[0] ?? null)} />
              </label>
              <button onClick={handleUpload} disabled={loading} className="rounded-md bg-slate-900 px-5 py-2 text-sm font-semibold text-white hover:bg-slate-700 disabled:opacity-50">
                {loading ? 'Import en cours...' : 'Importer'}
              </button>
            </div>
          </div>
          <div className="mt-4 flex flex-wrap items-center gap-3 text-sm text-slate-600">
            <span className="rounded-full bg-slate-100 px-3 py-1">Fichier : {file?.name ?? 'aucun fichier sélectionné'}</span>
            {message && <span className="text-slate-700">{message}</span>}
          </div>
        </section>
      </main>
    </div>
  )
}
