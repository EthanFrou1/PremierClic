import React from 'react'
import { Link, useLocation } from 'react-router-dom'

export default function Header() {
  const location = useLocation()
  const isProspects = location.pathname.startsWith('/prospects')

  return (
    <header className="border-b border-slate-200 bg-white">
      <div className="mx-auto flex max-w-6xl items-center justify-between px-6 py-4">
        <Link to="/" className="inline-flex items-center gap-2 rounded-full bg-sky-100 px-3 py-1 text-sm font-semibold text-sky-700">
          PremierClic
        </Link>
        <nav className="flex items-center gap-3 text-sm">
          <Link
            to="/"
            className={location.pathname === '/' ? 'font-semibold text-slate-900' : 'text-slate-600 hover:text-slate-900'}
          >
            Accueil
          </Link>
          <Link
            to="/prospects"
            className={`rounded-md px-4 py-2 font-medium ${isProspects ? 'bg-slate-900 text-white' : 'text-slate-600 hover:bg-slate-100'}`}
          >
            Prospects
          </Link>
        </nav>
      </div>
    </header>
  )
}
