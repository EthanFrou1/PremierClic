import React, { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { googleBusinessUrl } from '../utils/google'
import { STATUS_ORDER, statusMeta } from '../utils/status'

export default function ProspectDetail() {
  const { id } = useParams()
  const [prospect, setProspect] = useState(null)
  const [mockups, setMockups] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [saving, setSaving] = useState(false)
  const [emailState, setEmailState] = useState({ subject: '', bodyHtml: '' })
  const [mockupComment, setMockupComment] = useState('')
  const [mockupUrl, setMockupUrl] = useState('')
  const [uploadFile, setUploadFile] = useState(null)
  const [message, setMessage] = useState('')
  const [htmlFile, setHtmlFile] = useState(null)
  const [deploying, setDeploying] = useState(false)
  const [deployMessage, setDeployMessage] = useState('')
  const [refreshing, setRefreshing] = useState(false)
  const [refreshMessage, setRefreshMessage] = useState('')
  const [mockupPrompt, setMockupPrompt] = useState('')
  const [mockupPromptLoading, setMockupPromptLoading] = useState(false)
  const [mockupPromptError, setMockupPromptError] = useState('')
  const [mockupPromptCopied, setMockupPromptCopied] = useState(false)
  const [emailPrepared, setEmailPrepared] = useState(null)
  const [emailPreparing, setEmailPreparing] = useState(false)
  const [emailMarking, setEmailMarking] = useState(false)
  const [emailCopiedField, setEmailCopiedField] = useState(null)
  const [photoLinks, setPhotoLinks] = useState([])
  const [photoLinkInput, setPhotoLinkInput] = useState('')
  const [photoLinkAdding, setPhotoLinkAdding] = useState(false)
  const [photoLinkMessage, setPhotoLinkMessage] = useState('')

  useEffect(() => {
    fetchData()
  }, [id])

  async function fetchData() {
    setLoading(true)
    setError('')
    try {
      const res = await fetch((import.meta.env.VITE_API_BASE_URL || '') + `/api/prospects/${id}`)
      if (!res.ok) throw new Error('Impossible de charger le prospect')
      const data = await res.json()
      setProspect(data)
      await fetchMockups()
      await fetchPhotoLinks()
    } catch (err) {
      setError(err.message)
    } finally {
      setLoading(false)
    }
  }

  async function fetchMockups() {
    const res = await fetch((import.meta.env.VITE_API_BASE_URL || '') + `/api/prospects/${id}/mockups`)
    if (!res.ok) return
    const data = await res.json()
    setMockups(data)
  }

  async function fetchPhotoLinks() {
    const res = await fetch((import.meta.env.VITE_API_BASE_URL || '') + `/api/prospects/${id}/prospectphotolinks`)
    if (!res.ok) return
    const data = await res.json()
    setPhotoLinks(data)
  }

  async function handleAddPhotoLink() {
    setPhotoLinkMessage('')
    if (!photoLinkInput.trim()) {
      setPhotoLinkMessage('Colle un lien d\'image.')
      return
    }

    setPhotoLinkAdding(true)
    try {
      const res = await fetch((import.meta.env.VITE_API_BASE_URL || '') + `/api/prospects/${id}/prospectphotolinks`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ url: photoLinkInput.trim() })
      })
      if (!res.ok) {
        const text = await res.text()
        throw new Error(text || 'Échec de l\'ajout du lien')
      }
      setPhotoLinkInput('')
      await fetchPhotoLinks()
    } catch (err) {
      setPhotoLinkMessage(err.message)
    } finally {
      setPhotoLinkAdding(false)
    }
  }

  async function handleDeletePhotoLink(linkId) {
    try {
      const res = await fetch((import.meta.env.VITE_API_BASE_URL || '') + `/api/prospects/${id}/prospectphotolinks/${linkId}`, {
        method: 'DELETE'
      })
      if (!res.ok) throw new Error('Échec de la suppression')
      await fetchPhotoLinks()
    } catch (err) {
      setPhotoLinkMessage(err.message)
    }
  }

  async function handleSave() {
    if (!prospect) return
    setSaving(true)
    setMessage('')
    try {
      const res = await fetch((import.meta.env.VITE_API_BASE_URL || '') + `/api/prospects/${id}`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(prospect)
      })
      if (!res.ok) throw new Error('Erreur lors de la sauvegarde')
      setMessage('Prospect mis à jour.')
    } catch (err) {
      setMessage(err.message)
    } finally {
      setSaving(false)
    }
  }

  async function handleAddMockup() {
    setMessage('')
    if (!mockupUrl && !uploadFile) {
      setMessage('Ajoute une URL ou un fichier de maquette.')
      return
    }

    try {
      if (uploadFile) {
        const formData = new FormData()
        formData.append('file', uploadFile)
        formData.append('commentaire', mockupComment)
        const res = await fetch((import.meta.env.VITE_API_BASE_URL || '') + `/api/prospects/${id}/mockups/upload`, {
          method: 'POST',
          body: formData
        })
        if (!res.ok) throw new Error('Échec du téléchargement')
      } else {
        const res = await fetch((import.meta.env.VITE_API_BASE_URL || '') + `/api/prospects/${id}/mockups`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ urlPreview: mockupUrl, commentaire: mockupComment })
        })
        if (!res.ok) throw new Error('Échec de l’ajout de la maquette')
      }
      setMockupUrl('')
      setMockupComment('')
      setUploadFile(null)
      setMessage('Maquette ajoutée.')
      await fetchMockups()
    } catch (err) {
      setMessage(err.message)
    }
  }

  async function handleDeployToNetlify() {
    setDeployMessage('')
    if (!htmlFile) {
      setDeployMessage('Choisis le fichier HTML exporté de Claude Design.')
      return
    }

    setDeploying(true)
    try {
      const formData = new FormData()
      formData.append('file', htmlFile)
      formData.append('commentaire', mockupComment)
      const res = await fetch((import.meta.env.VITE_API_BASE_URL || '') + `/api/prospects/${id}/mockups/deploy`, {
        method: 'POST',
        body: formData
      })
      if (!res.ok) {
        const text = await res.text()
        throw new Error(text || 'Échec du déploiement Netlify')
      }
      setHtmlFile(null)
      setMockupComment('')
      setDeployMessage('Maquette déployée sur Netlify.')
      await fetchMockups()
    } catch (err) {
      setDeployMessage(err.message)
    } finally {
      setDeploying(false)
    }
  }

  async function handleRefreshGoogle() {
    setRefreshing(true)
    setRefreshMessage('')
    try {
      const res = await fetch((import.meta.env.VITE_API_BASE_URL || '') + `/api/prospects/discover/google/refresh/${id}`, {
        method: 'POST'
      })
      if (!res.ok) {
        const text = await res.text()
        throw new Error(text || 'Erreur de rafraîchissement')
      }
      const payload = await res.json()
      if (!payload.found) {
        setRefreshMessage('Commerce introuvable sur Google Places.')
      } else if (payload.updated) {
        setProspect(payload.prospect)
        setRefreshMessage('Fiche mise à jour depuis Google.')
      } else {
        setRefreshMessage('Déjà à jour, rien à compléter.')
      }
    } catch (err) {
      setRefreshMessage(err.message || 'Erreur inconnue')
    } finally {
      setRefreshing(false)
    }
  }

  async function handleGenerateMockupPrompt() {
    setMockupPromptLoading(true)
    setMockupPromptError('')
    setMockupPromptCopied(false)
    try {
      const res = await fetch((import.meta.env.VITE_API_BASE_URL || '') + `/api/prospects/discover/google/mockup-prompt/${id}`)
      if (!res.ok) {
        const text = await res.text()
        throw new Error(text || 'Erreur de génération du prompt')
      }
      const payload = await res.json()
      if (!payload.found) {
        setMockupPrompt('')
        setMockupPromptError('Commerce introuvable sur Google Places, impossible de générer le prompt.')
      } else {
        setMockupPrompt(payload.prompt)
      }
    } catch (err) {
      setMockupPromptError(err.message || 'Erreur inconnue')
    } finally {
      setMockupPromptLoading(false)
    }
  }

  async function handleCopyMockupPrompt() {
    if (!mockupPrompt) return
    try {
      await navigator.clipboard.writeText(mockupPrompt)
      setMockupPromptCopied(true)
      setTimeout(() => setMockupPromptCopied(false), 2000)
    } catch {
      setMockupPromptError('Impossible de copier automatiquement, sélectionne le texte manuellement.')
    }
  }

  async function handlePrepareEmail() {
    if (!prospect) return
    setMessage('')
    setEmailPreparing(true)
    try {
      const res = await fetch((import.meta.env.VITE_API_BASE_URL || '') + '/api/email/prepare', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ prospectId: id, subject: emailState.subject, bodyHtml: emailState.bodyHtml })
      })
      if (!res.ok) {
        const text = await res.text()
        throw new Error(text || 'Erreur de préparation de l\'email')
      }
      const payload = await res.json()
      setEmailPrepared(payload)
    } catch (err) {
      setMessage(err.message)
    } finally {
      setEmailPreparing(false)
    }
  }

  async function handleCopyEmailField(field, value) {
    try {
      await navigator.clipboard.writeText(value)
      setEmailCopiedField(field)
      setTimeout(() => setEmailCopiedField(null), 2000)
    } catch {
      setMessage('Impossible de copier automatiquement, sélectionne le texte manuellement.')
    }
  }

  async function handleMarkSent() {
    if (!prospect) return
    setEmailMarking(true)
    setMessage('')
    try {
      const res = await fetch((import.meta.env.VITE_API_BASE_URL || '') + '/api/email/mark-sent', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ prospectId: id, subject: emailState.subject, bodyHtml: emailState.bodyHtml })
      })
      if (!res.ok) {
        const text = await res.text()
        throw new Error(text || 'Erreur lors du marquage')
      }
      setMessage('Email marqué comme envoyé.')
      setEmailPrepared(null)
    } catch (err) {
      setMessage(err.message)
    } finally {
      setEmailMarking(false)
    }
  }

  if (loading) return <div className="p-6">Chargement...</div>
  if (error) return <div className="p-6 text-red-600">{error}</div>
  if (!prospect) return <div className="p-6">Prospect introuvable</div>

  return (
    <div className="p-6">
      <div className="mb-6 flex items-center justify-between gap-4">
        <div>
          <h2 className="text-2xl font-semibold">{prospect.nom}</h2>
          <p className="text-sm text-slate-500">{prospect.ville} · {prospect.categorie}</p>
          <div className="mt-1 flex flex-wrap items-center gap-3">
            <a
              href={googleBusinessUrl(prospect)}
              target="_blank"
              rel="noreferrer"
              className="text-sm font-medium text-sky-700 hover:underline"
            >
              Voir la fiche Google Business →
            </a>
            <button
              onClick={handleRefreshGoogle}
              disabled={refreshing}
              className="rounded-full bg-amber-100 px-3 py-1 text-xs font-semibold text-amber-800 hover:bg-amber-200 disabled:opacity-50"
            >
              {refreshing ? 'Rafraîchissement...' : 'Rafraîchir depuis Google'}
            </button>
          </div>
          {refreshMessage && <p className="mt-1 text-xs text-slate-500">{refreshMessage}</p>}
        </div>
        <Link to="/prospects" className="rounded-md bg-slate-900 px-4 py-2 text-sm font-semibold text-white">Retour</Link>
      </div>

      <div className="grid gap-6 lg:grid-cols-[1.3fr_0.9fr]">
        <section className="space-y-4 rounded-3xl bg-white p-6 shadow-sm shadow-slate-200">
          <h3 className="text-lg font-semibold">Détails du prospect</h3>
          <div className="grid gap-4 sm:grid-cols-2">
            {[
              { label: 'Nom', key: 'nom' },
              { label: 'Email', key: 'email' },
              { label: 'Téléphone', key: 'telephone' },
              { label: 'Ville', key: 'ville' },
              { label: 'Adresse', key: 'adresse' },
              { label: 'Code postal', key: 'codePostal' },
            ].map(field => (
              <label key={field.key} className="space-y-1 text-sm">
                <span className="font-medium text-slate-700">{field.label}</span>
                <input
                  value={prospect[field.key] || ''}
                  onChange={e => setProspect({ ...prospect, [field.key]: e.target.value })}
                  className="w-full rounded-xl border border-slate-200 bg-slate-50 px-3 py-2"
                />
              </label>
            ))}
          </div>

          <div className="grid gap-4 sm:grid-cols-2">
            <label className="space-y-1 text-sm">
              <span className="font-medium text-slate-700">Statut</span>
              <select
                value={prospect.statut}
                onChange={e => setProspect({ ...prospect, statut: e.target.value })}
                className="w-full rounded-xl border border-slate-200 bg-slate-50 px-3 py-2"
              >
                {STATUS_ORDER.map(option => <option key={option} value={option}>{statusMeta(option).label}</option>)}
              </select>
            </label>
            <label className="space-y-1 text-sm">
              <span className="font-medium text-slate-700">Catégorie</span>
              <input
                value={prospect.categorie || ''}
                onChange={e => setProspect({ ...prospect, categorie: e.target.value })}
                className="w-full rounded-xl border border-slate-200 bg-slate-50 px-3 py-2"
              />
            </label>
          </div>

          <label className="space-y-1 text-sm">
            <span className="font-medium text-slate-700">Source de données</span>
            <input
              value={prospect.sourceDonnees || ''}
              onChange={e => setProspect({ ...prospect, sourceDonnees: e.target.value })}
              className="w-full rounded-xl border border-slate-200 bg-slate-50 px-3 py-2"
            />
          </label>

          <label className="space-y-1 text-sm">
            <span className="font-medium text-slate-700">Notes</span>
            <textarea
              value={prospect.notes || ''}
              onChange={e => setProspect({ ...prospect, notes: e.target.value })}
              rows={4}
              className="w-full rounded-xl border border-slate-200 bg-slate-50 px-3 py-2"
            />
          </label>

          <button onClick={handleSave} disabled={saving} className="rounded-xl bg-slate-900 px-5 py-3 text-sm font-semibold text-white hover:bg-slate-700 disabled:opacity-50">
            {saving ? 'Enregistrement...' : 'Enregistrer les modifications'}
          </button>
          {message && <div className="mt-3 text-sm text-slate-700">{message}</div>}
        </section>

        <aside className="space-y-6">
          <section className="rounded-3xl bg-white p-6 shadow-sm shadow-slate-200">
            <h3 className="text-lg font-semibold">Prompt maquette</h3>
            <p className="text-sm text-slate-500">Génère un prompt prêt à coller dans Claude, avec les infos et avis récupérés depuis la fiche Google Business.</p>
            <button
              onClick={handleGenerateMockupPrompt}
              disabled={mockupPromptLoading}
              className="mt-3 w-full rounded-xl bg-slate-900 px-5 py-3 text-sm font-semibold text-white hover:bg-slate-700 disabled:opacity-50"
            >
              {mockupPromptLoading ? 'Génération...' : 'Générer le prompt'}
            </button>
            {mockupPromptError && <p className="mt-2 text-sm text-rose-700">{mockupPromptError}</p>}
            {mockupPrompt && (
              <div className="mt-4 space-y-2">
                <textarea
                  readOnly
                  value={mockupPrompt}
                  rows={10}
                  className="w-full rounded-xl border border-slate-200 bg-slate-50 px-3 py-2 text-xs text-slate-700"
                />
                <button
                  onClick={handleCopyMockupPrompt}
                  className="w-full rounded-xl bg-sky-600 px-4 py-2 text-sm font-semibold text-white hover:bg-sky-500"
                >
                  {mockupPromptCopied ? 'Copié !' : 'Copier le prompt'}
                </button>
              </div>
            )}
          </section>

          <section className="rounded-3xl bg-white p-6 shadow-sm shadow-slate-200">
            <h3 className="text-lg font-semibold">Liens photos</h3>
            <p className="text-sm text-slate-500">Colle des liens directs vers des photos (ex. via clic droit → Inspecter sur une photo Google Maps, puis copie l'URL de l'image). Ils seront inclus dans le prompt de maquette.</p>
            <div className="mt-3 flex gap-2">
              <input
                value={photoLinkInput}
                onChange={e => setPhotoLinkInput(e.target.value)}
                placeholder="https://lh3.googleusercontent.com/..."
                className="w-full rounded-xl border border-slate-200 bg-slate-50 px-3 py-2 text-sm"
              />
              <button
                onClick={handleAddPhotoLink}
                disabled={photoLinkAdding}
                className="shrink-0 rounded-xl bg-slate-900 px-4 py-2 text-sm font-semibold text-white hover:bg-slate-700 disabled:opacity-50"
              >
                {photoLinkAdding ? 'Ajout...' : 'Ajouter'}
              </button>
            </div>
            {photoLinkMessage && <p className="mt-2 text-sm text-slate-700">{photoLinkMessage}</p>}
            {photoLinks.length > 0 && (
              <div className="mt-4 grid grid-cols-3 gap-2">
                {photoLinks.map(p => (
                  <div key={p.id} className="relative overflow-hidden rounded-xl border border-slate-200">
                    <img src={p.url} alt="Lien photo" className="h-20 w-full object-cover" />
                    <button
                      onClick={() => handleDeletePhotoLink(p.id)}
                      title="Supprimer"
                      className="absolute right-1 top-1 rounded-full bg-black/60 px-1.5 text-xs font-semibold text-white hover:bg-black/80"
                    >
                      ×
                    </button>
                  </div>
                ))}
              </div>
            )}
          </section>

          <section className="rounded-3xl bg-white p-6 shadow-sm shadow-slate-200">
            <h3 className="text-lg font-semibold">Maquettes</h3>
            <div className="space-y-3">
              <label className="block text-sm font-medium text-slate-700">Lien de maquette</label>
              <input
                value={mockupUrl}
                onChange={e => setMockupUrl(e.target.value)}
                placeholder="https://..."
                className="w-full rounded-xl border border-slate-200 bg-slate-50 px-3 py-2"
              />
              <label className="block text-sm font-medium text-slate-700">Commentaire</label>
              <input
                value={mockupComment}
                onChange={e => setMockupComment(e.target.value)}
                className="w-full rounded-xl border border-slate-200 bg-slate-50 px-3 py-2"
              />
              <label className="block text-sm font-medium text-slate-700">Ou upload d’image</label>
              <input type="file" accept="image/*" onChange={e => setUploadFile(e.target.files?.[0] || null)} className="w-full text-sm text-slate-700" />
              <button onClick={handleAddMockup} className="mt-3 w-full rounded-xl bg-slate-900 px-5 py-3 text-sm font-semibold text-white hover:bg-slate-700">Ajouter maquette</button>
            </div>
            <div className="mt-5 border-t border-slate-100 pt-5 space-y-3">
              <label className="block text-sm font-medium text-slate-700">Déployer un export HTML (Claude Design) sur Netlify</label>
              <input type="file" accept=".html" onChange={e => setHtmlFile(e.target.files?.[0] || null)} className="w-full text-sm text-slate-700" />
              <button
                onClick={handleDeployToNetlify}
                disabled={deploying}
                className="w-full rounded-xl bg-teal-600 px-5 py-3 text-sm font-semibold text-white hover:bg-teal-500 disabled:opacity-50"
              >
                {deploying ? 'Déploiement...' : 'Déployer sur Netlify'}
              </button>
              {deployMessage && <p className="text-sm text-slate-700">{deployMessage}</p>}
            </div>
            <div className="mt-5 space-y-3">
              {mockups.length === 0 && <div className="text-sm text-slate-500">Aucune maquette ajoutée.</div>}
              {mockups.map(m => (
                <div key={m.id} className="rounded-2xl border border-slate-200 bg-slate-50 p-3">
                  <p className="text-sm font-semibold text-slate-800">{m.commentaire || 'Maquette'}</p>
                  {m.urlPreview && <a href={m.urlPreview} target="_blank" rel="noreferrer" className="text-sm text-sky-700">Voir la maquette</a>}
                  {m.path && <span className="text-sm text-slate-600 block">Fichier uploadé : {m.path}</span>}
                </div>
              ))}
            </div>
          </section>

          <section className="rounded-3xl bg-white p-6 shadow-sm shadow-slate-200">
            <h3 className="text-lg font-semibold">Email de prospection</h3>
            <label className="block text-sm font-medium text-slate-700">Sujet</label>
            <input
              value={emailState.subject}
              onChange={e => setEmailState({ ...emailState, subject: e.target.value })}
              placeholder="Sujet de l'email"
              className="mt-1 w-full rounded-xl border border-slate-200 bg-slate-50 px-3 py-2"
            />
            <label className="mt-4 block text-sm font-medium text-slate-700">Contenu HTML</label>
            <textarea
              value={emailState.bodyHtml}
              onChange={e => setEmailState({ ...emailState, bodyHtml: e.target.value })}
              rows={6}
              placeholder="Bonjour {{nom}}, ..."
              className="mt-1 w-full rounded-xl border border-slate-200 bg-slate-50 px-3 py-2"
            />
            <button
              onClick={handlePrepareEmail}
              disabled={emailPreparing}
              className="mt-4 w-full rounded-xl bg-emerald-600 px-5 py-3 text-sm font-semibold text-white hover:bg-emerald-500 disabled:opacity-50"
            >
              {emailPreparing ? 'Préparation...' : 'Préparer l\'email'}
            </button>
            <p className="mt-3 text-xs text-slate-500">{"Utilise {{nom}} et {{unsubscribeUrl}} dans le sujet ou le corps pour personnaliser."}</p>

            {emailPrepared && (
              <div className="mt-4 space-y-3 rounded-2xl border border-slate-200 bg-slate-50 p-4">
                <div>
                  <div className="flex items-center justify-between">
                    <span className="text-xs font-semibold uppercase tracking-wide text-slate-500">Objet</span>
                    <button
                      onClick={() => handleCopyEmailField('subject', emailPrepared.subject)}
                      className="text-xs font-semibold text-sky-700 hover:underline"
                    >
                      {emailCopiedField === 'subject' ? 'Copié !' : 'Copier'}
                    </button>
                  </div>
                  <p className="mt-1 text-sm text-slate-800">{emailPrepared.subject}</p>
                </div>
                <div>
                  <div className="flex items-center justify-between">
                    <span className="text-xs font-semibold uppercase tracking-wide text-slate-500">Corps</span>
                    <button
                      onClick={() => handleCopyEmailField('body', emailPrepared.bodyHtml)}
                      className="text-xs font-semibold text-sky-700 hover:underline"
                    >
                      {emailCopiedField === 'body' ? 'Copié !' : 'Copier'}
                    </button>
                  </div>
                  <textarea readOnly value={emailPrepared.bodyHtml} rows={6} className="mt-1 w-full rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm text-slate-700" />
                </div>
                <p className="text-xs text-slate-500">Colle l'objet et le corps dans ton client mail (Gmail...), envoie depuis ta boîte, puis confirme ici.</p>
                <button
                  onClick={handleMarkSent}
                  disabled={emailMarking}
                  className="w-full rounded-xl bg-slate-900 px-4 py-2 text-sm font-semibold text-white hover:bg-slate-700 disabled:opacity-50"
                >
                  {emailMarking ? 'Enregistrement...' : 'Marquer comme envoyé'}
                </button>
              </div>
            )}
          </section>
        </aside>
      </div>
    </div>
  )
}
