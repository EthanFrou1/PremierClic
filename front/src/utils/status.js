export const STATUS_META = {
  ANouveauFait: { label: 'Nouveau', dot: 'bg-slate-400', row: 'bg-slate-50', text: 'text-slate-700' },
  AContacter: { label: 'À contacter', dot: 'bg-sky-500', row: 'bg-sky-50', text: 'text-sky-700' },
  Contacte: { label: 'Contacté', dot: 'bg-amber-500', row: 'bg-amber-50', text: 'text-amber-700' },
  Relance: { label: 'Relance', dot: 'bg-orange-500', row: 'bg-orange-50', text: 'text-orange-700' },
  'Interessé': { label: 'Intéressé', dot: 'bg-emerald-500', row: 'bg-emerald-50', text: 'text-emerald-700' },
  PasInteresse: { label: 'Pas intéressé', dot: 'bg-rose-400', row: 'bg-rose-50', text: 'text-rose-700' },
  Client: { label: 'Client', dot: 'bg-green-600', row: 'bg-green-50', text: 'text-green-800' },
  DesinscritOptOut: { label: 'Désinscrit', dot: 'bg-zinc-400', row: 'bg-zinc-100', text: 'text-zinc-500' },
}

export const STATUS_ORDER = [
  'ANouveauFait', 'AContacter', 'Contacte', 'Relance', 'Interessé', 'PasInteresse', 'Client', 'DesinscritOptOut'
]

export function statusMeta(statut) {
  return STATUS_META[statut] || { label: statut, dot: 'bg-slate-400', row: '', text: 'text-slate-700' }
}
