let loadPromise = null

export function loadGoogleMapsScript() {
  if (window.google?.maps?.places) return Promise.resolve(window.google)
  if (loadPromise) return loadPromise

  loadPromise = new Promise((resolve, reject) => {
    const apiKey = import.meta.env.VITE_GOOGLE_MAPS_API_KEY
    if (!apiKey) {
      reject(new Error('VITE_GOOGLE_MAPS_API_KEY n\'est pas configurée.'))
      return
    }
    const script = document.createElement('script')
    script.src = `https://maps.googleapis.com/maps/api/js?key=${apiKey}&libraries=places`
    script.async = true
    script.onload = () => resolve(window.google)
    script.onerror = () => reject(new Error('Échec du chargement du script Google Maps.'))
    document.head.appendChild(script)
  })

  return loadPromise
}
