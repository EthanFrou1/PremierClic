<#
Deploie une maquette HTML (export Claude Design) sur Netlify et affiche le lien
public a partager avec un prospect. Chaque deploiement genere un lien unique et
permanent (rien n'est ecrase), donc tu peux relancer le script pour chaque prospect.

Usage:
  .\deploy-maquette.ps1 -Fichier "C:\chemin\vers\maquette.html"

Prerequis, a faire UNE SEULE FOIS avant la premiere utilisation:
  1. npm install -g netlify-cli
  2. npx netlify-cli login                            (ouvre le navigateur pour autoriser)
  3. npx netlify-cli sites:create --name premierclic-maquettes
     -> note le "Site ID" affiche dans le resultat
  4. Ajoute cette ligne a ton profil PowerShell (notepad $PROFILE) pour ne pas
     avoir a la retaper a chaque fois:
       $env:NETLIFY_SITE_ID = "colle-ton-site-id-ici"
     Puis rouvre un nouveau terminal.

Note: le script utilise "npx netlify-cli" plutot que "netlify" directement, car
la commande netlify seule n'est pas toujours trouvee dans le PATH selon les
installations (npx la retrouve sans probleme).
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$Fichier
)

if (-not (Get-Command npx -ErrorAction SilentlyContinue)) {
    Write-Error "npx introuvable (Node.js n'est pas installe ou pas dans le PATH)."
    exit 1
}

if (-not (Test-Path $Fichier)) {
    Write-Error "Fichier introuvable : $Fichier"
    exit 1
}

if (-not $env:NETLIFY_SITE_ID) {
    Write-Error "Variable NETLIFY_SITE_ID non definie. Fais d'abord le setup en une fois (voir en-tete du script)."
    exit 1
}

$temp = Join-Path $env:TEMP ("maquette-" + [guid]::NewGuid())
New-Item -ItemType Directory -Path $temp | Out-Null
Copy-Item $Fichier (Join-Path $temp "index.html")

try {
    Write-Host "Deploiement en cours sur Netlify..." -ForegroundColor Cyan
    $raw = npx netlify-cli deploy --dir $temp --site $env:NETLIFY_SITE_ID --json
    $result = $raw | ConvertFrom-Json

    if ($result.deploy_url) {
        Write-Host ""
        Write-Host "Lien a partager avec le prospect :" -ForegroundColor Green
        Write-Host $result.deploy_url -ForegroundColor Yellow
        Write-Host ""
        Set-Clipboard -Value $result.deploy_url
        Write-Host "(Le lien a ete copie dans le presse-papiers)" -ForegroundColor DarkGray
    } else {
        Write-Warning "Deploiement termine mais aucun lien trouve dans la reponse. Sortie brute:"
        Write-Host $raw
    }
} finally {
    Remove-Item $temp -Recurse -Force -ErrorAction SilentlyContinue
}
