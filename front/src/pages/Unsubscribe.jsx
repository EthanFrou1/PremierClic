import React, { useEffect, useState } from 'react'
import { useParams, Link } from 'react-router-dom'

export default function Unsubscribe() {
  const { token } = useParams()
  const [message, setMessage] = useState('')
  const [status, setStatus] = useState('loading')

  useEffect(() => {
    if (!token) return
    fetch((import.meta.env.VITE_API_BASE_URL || '') + `/api/email/unsubscribe/${token}`)
      .then(async res => {
        if (!res.ok) {
          const body = await res.text()
          throw new Error(body || 'Impossible de se désinscrire')
        }
        return res.json()
      })
      .then(data => {
        setMessage(data.message || 'Vous êtes désinscrit.')
        setStatus('success')
      })
      .catch(err => {
        setMessage(err.message)
        setStatus('error')
      })
  }, [token])

  return (
    <div className="min-h-screen bg-slate-50 px-6 py-16 text-slate-900">
      <div className="mx-auto max-w-3xl rounded-3xl bg-white p-10 shadow-lg shadow-slate-200">
        <h1 className="text-3xl font-semibold">Désinscription</h1>
        <p className="mt-4 text-slate-600">{status === 'loading' ? 'Traitement en cours...' : message}</p>
        <div className="mt-6">
          <Link to="/" className="inline-flex rounded-xl bg-slate-900 px-5 py-3 text-sm font-semibold text-white hover:bg-slate-700">Retour à l'accueil</Link>
        </div>
      </div>
    </div>
  )
}
