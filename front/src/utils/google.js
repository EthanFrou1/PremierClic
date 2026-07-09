export function googleBusinessUrl(prospect) {
  if (prospect.googlePlaceId) {
    return `https://www.google.com/maps/place/?q=place_id:${prospect.googlePlaceId}`
  }
  const query = [prospect.nom, prospect.adresse || prospect.ville].filter(Boolean).join(' ')
  return `https://www.google.com/maps/search/?api=1&query=${encodeURIComponent(query)}`
}
