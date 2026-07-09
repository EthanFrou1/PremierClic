# Projet PremierClic

## Objectif du projet

PremierClic est un outil de prospection locale pour aider les petits commerces qui n'ont pas encore de site internet. Le but est de pouvoir démarcher ces commerces rapidement, sans travail manuel excessif, en leur proposant une maquette de site ou une proposition de design (via Claude Design plus tard), puis en envoyant cette proposition par email.

L'application doit faciliter :
- la découverte et l'import de prospects locaux,
- la centralisation de leurs informations,
- la préparation de propositions visuelles à leur envoyer,
- le suivi du démarchage commercial.

## Fonctionnalités déjà mises en place

### Infrastructure
- Application multi-services avec Docker Compose.
- Services principaux :
  - `db` : base de données PostgreSQL.
  - `api` : back-end ASP.NET Core.
  - `front` : front-end React + Vite + Tailwind.

### Backend (`/api`)
- API REST pour gérer les prospects.
- Authentification de connexion avec un endpoint `api/auth/login`.
- Modèles EF Core et migrations pour la structure de données.
- Endpoints CRUD pour les prospects : création, lecture, mise à jour, suppression.
- Recherche de prospects avec filtres par statut, catégorie et ville.
- Import automatique de prospects via CSV ou JSON.
- Fonctionnalités de découverte automatique : intégration d'une recherche locale (Overpass / Google Places) et import de commerces.
- Résolution automatique de la fiche Google Place d'un prospect (`place_id`), avec repli sur une recherche par nom/adresse (Find Place From Text) si l'identifiant n'est pas connu.
- Génération automatique d'un **prompt de maquette prêt à coller dans Claude Design** (`GET /api/prospects/discover/google/mockup-prompt/{id}`), construit à partir de la fiche Google Business du prospect :
  - coordonnées, horaires d'ouverture, adresse,
  - avis clients les mieux notés (filtrés ≥ 4 étoiles, tronqués et priorisés par concision),
  - informations pratiques via l'API Google Places (New) : accessibilité, moyens de paiement, stationnement,
  - photos officielles de l'établissement uniquement (détection par correspondance du nom de l'auteur avec le nom du commerce, pour exclure les photos d'avis clients),
  - consignes intégrées au prompt pour un rendu mobile-first et sans bug de scroll.
- Préparation d'emails de prospection à envoyer manuellement (`POST /api/email/prepare` puis `POST /api/email/mark-sent`) : l'objet et le corps (avec templates et placeholders `{{nom}}`, `{{email}}`, `{{unsubscribeUrl}}`) sont générés côté back-end pour être copiés dans sa propre boîte mail, plutôt qu'un envoi automatique via SMTP — meilleure délivrabilité et suivi CRM conservé via l'enregistrement `EmailEnvoye`.
- Gestion de templates d'email.
- **Déploiement automatique des maquettes sur Netlify depuis l'app** (`POST /api/prospects/{id}/mockups/deploy`) : upload d'un export HTML (Claude Design), zippé à la volée côté serveur et poussé vers l'API Netlify (`/sites/{siteId}/deploys`, auth par `NETLIFY_API_TOKEN`/`NETLIFY_SITE_ID`), le lien de déploiement obtenu est enregistré automatiquement comme `Mockup` — plus besoin de coller le lien à la main.
- **Photos personnalisées par prospect** (`ProspectPhotosController`, modèle `ProspectPhoto`) : upload multi-fichiers, liste et suppression (`GET/POST/DELETE /api/prospects/{id}/prospectphotos`), fichiers servis en statique via `/uploads/photos/...`. Ces photos sont automatiquement ajoutées au prompt de maquette (section « Photos supplémentaires sélectionnées manuellement »), en complément des photos officielles Google.

### Front-end (`/front`)
- Page de connexion sécurisée.
- Interface prospects :
  - affichage de la liste des prospects,
  - formulaire de création de prospect avec autocomplétion de ville via le widget natif `google.maps.places.Autocomplete`,
  - filtres de recherche,
  - découverte automatique de prospects locaux.
- Page de détail prospect (`/prospects/:id`) :
  - section « Prompt maquette » pour générer et copier en un clic le prompt Claude Design du prospect,
  - section « Photos personnalisées » pour uploader ses propres photos de l'établissement (multi-fichiers, aperçu en grille, suppression), automatiquement injectées dans le prompt de maquette,
  - section « Email de prospection » pour préparer l'objet/corps, copier chaque champ individuellement, puis confirmer l'envoi manuel via « Marquer comme envoyé »,
  - section « Maquettes » pour centraliser les liens de maquettes livrées, avec un bouton « Déployer sur Netlify » qui prend en entrée l'export HTML de Claude Design et enregistre automatiquement le lien public une fois le déploiement terminé.
- Gestion des états de chargement et des erreurs.
- Navigation basique entre les écrans.

### Outils annexes
- Script `deploy-maquette.ps1` (racine du repo) : déploie une maquette HTML (export Claude Design) sur Netlify en une seule commande et copie automatiquement le lien public partageable dans le presse-papiers. Conservé comme solution de secours en ligne de commande, mais désormais complémentaire au bouton « Déployer sur Netlify » intégré dans l'app (plus besoin de setup Netlify CLI pour l'usage courant).
- Site Netlify dédié `premierclic-maquettes` (id `0cd04bcc-b7e3-4225-a0ba-45a1c9b94b9a`) utilisé comme cible de déploiement par le bouton in-app et par le script.

### Données et modèles
- Modèles métier existants : Prospect, User, EmailTemplate, EmailEnvoye, Mockup, ProspectPhoto.
- Migrations EF Core pour initialiser la base de données.

## Points forts actuels

- Architecture full-stack claire entre `api` et `front`.
- Déploiement local reproductible via Docker Compose.
- Fonctionnalités de base de prospection opérationnelles.
- Import et découverte automatique déjà disponibles.
- Chaîne de prospection quasi automatisée de bout en bout : découverte → enrichissement Google → prompt de maquette (+ photos perso) → maquette déployée sur Netlify en un clic → email prêt à envoyer, avec un minimum de saisie manuelle.

## Idées et améliorations à faire plus tard

### Fonctionnalités métiers
- Ajouter la suppression et la mise à jour complète de prospects côté front.
- Ajouter des fiches clients avec historique de contact.
- Ajouter des catégories métiers et des labels personnalisés.
- Ajouter un tableau de bord avec statistiques de conversion.
- Ajouter un module de suivi des relances et des actions commerciales.
- Ajouter un historique complet des emails envoyés par prospect (au-delà du simple "marquer comme envoyé") et des relances automatiques.
- ~~Enregistrer automatiquement le lien de la maquette déployée (Netlify) dans la fiche prospect~~ → fait (bouton « Déployer sur Netlify » in-app).
- Une fois l'API déployée avec un nom de domaine public (Coolify), les URLs de photos personnalisées (`/uploads/photos/...`) deviendront directement accessibles à Claude Design ; en local (`localhost`), Claude Design ne peut pas encore les télécharger automatiquement depuis le prompt généré.
- Permettre la suppression d'un Mockup depuis la fiche prospect (actuellement possible uniquement pour les ProspectPhoto).

### Qualité et sécurité
- Mettre en place une gestion des rôles (`admin`, `commercial`, `lecture seule`).
- Renforcer la sécurité JWT / tokens et la validation côté front.
- Ajouter des tests unitaires et d'intégration pour le back-end et le front.
- Ajouter une couverture d’API avec Swagger.

### UX / UI
- Améliorer le design de l’interface et l’ergonomie.
- Ajouter un tableau Kanban ou un filtre avancé pour suivre le pipeline.
- Ajouter une carte interactive pour visualiser les prospects géolocalisés.
- Ajouter un mode sombre.

### Déploiement et maintenance
- ~~Ajouter un fichier `.env.example` complet avec toutes les variables attendues~~ → fait (variables Netlify ajoutées ; une vraie clé Google Maps qui avait été committée par erreur dans `.env.example` a aussi été retirée et remplacée par un placeholder — `.env` reste correctement gitignoré).
- Créer une documentation de déploiement pour Coolify / Docker.
- Ajouter un script de backup de la base de données.
- Mettre en place un pipeline CI/CD pour tests et déploiement.

## Journal des sessions

```text
[DATE] : 09/07/2026

1) Tâches réalisées aujourd'hui
- [x] Création du vrai site Netlify de déploiement des maquettes (`premierclic-maquettes`, id `0cd04bcc-b7e3-4225-a0ba-45a1c9b94b9a`), configuration de `NETLIFY_API_TOKEN` / `NETLIFY_SITE_ID` (`.env`, `.env.example`, `docker-compose.yml`)
- [x] Bouton « Déployer sur Netlify » intégré dans la fiche prospect (`POST /api/prospects/{id}/mockups/deploy`) : upload d'un export HTML, zip côté serveur, envoi à l'API Netlify, enregistrement automatique du lien comme Mockup
- [x] Fonctionnalité « Photos personnalisées » : modèle `ProspectPhoto` + migration `AddProspectPhotos`, `ProspectPhotosController` (GET/POST/DELETE), fichiers servis en statique via `/uploads`, section front dédiée (upload multi, grille d'aperçu, suppression)
- [x] Intégration des photos personnalisées dans le prompt de maquette généré (`BuildMockupPrompt`)
- [x] Correctif de sécurité : une vraie clé Google Maps committée par erreur dans `.env.example` a été retirée et remplacée par un placeholder
- [x] Tests de bout en bout via curl (upload de photo, suppression, génération du prompt avec photos perso) contre les conteneurs Docker

2) Tâches en cours / à continuer
- [ ] Vérifier en conditions réelles le bouton « Déployer sur Netlify » avec un vrai export Claude Design
- [ ] Confirmer que Claude Design peut bien récupérer les photos personnalisées une fois l'API accessible publiquement (limite connue : URLs `localhost` non accessibles depuis l'extérieur)

3) Tâches à faire ensuite
- [ ] Documentation de déploiement Coolify / Docker (nom de domaine public pour l'API)
- [ ] Historique complet des emails envoyés par prospect
- [ ] Suppression d'un Mockup depuis la fiche prospect

4) Blocages / décisions prises
- [x] Décision : utiliser l'API de déploiement Netlify (zip direct) plutôt que le CLI depuis le back-end, pour permettre un déploiement en un clic depuis l'app sans dépendance à `netlify-cli` côté serveur
- [x] Décision : conserver `deploy-maquette.ps1` comme solution de secours en ligne de commande plutôt que de le supprimer

5) Notes / remarques
- Les photos personnalisées ne seront réellement exploitables par Claude Design qu'une fois l'API déployée avec une URL publique (actuellement `localhost` en dev).
```

```text
[DATE] : 08/07/2026

1) Tâches réalisées aujourd'hui
- [x] Remplacement de l'autocomplétion de ville maison par le widget natif google.maps.places.Autocomplete
- [x] Génération automatique d'un prompt de maquette Claude Design (horaires, avis, infos pratiques, photos officielles filtrées, consignes mobile-first/anti-scroll)
- [x] Intégration de l'API Google Places (New) pour les informations pratiques et les photos
- [x] Passage de l'envoi d'email en SMTP automatique à une préparation manuelle (prepare / mark-sent) copiable dans Gmail
- [x] Script deploy-maquette.ps1 pour déployer une maquette HTML sur Netlify et récupérer un lien partageable en une commande

2) Tâches en cours / à continuer
- [ ] Tester deploy-maquette.ps1 en conditions réelles (setup Netlify CLI + premier déploiement)
- [ ] Vérifier le format de sortie JSON de `netlify deploy` (champ deploy_url) selon la version du CLI

3) Tâches à faire ensuite
- [ ] Historique complet des emails envoyés par prospect
- [ ] Enregistrement automatique du lien de maquette déployé dans la fiche prospect

4) Blocages / décisions prises
- [x] Décision : envoi d'email manuel depuis Gmail plutôt qu'automatique (meilleure délivrabilité, pas de config SMTP à maintenir)
- [x] Décision : n'utiliser que les photos officielles de l'établissement dans le prompt de maquette (pas de fallback sur les photos d'avis clients)
- [ ] Limite connue : l'API Google Places ne distingue pas les catégories de photos (plats/atmosphère/récentes) visibles dans l'UI Google Maps

5) Notes / remarques
- Le lien "Anyone with the link" n'est pas disponible sur le plan Claude Design actuel ; Netlify Drop/CLI est la solution de contournement retenue pour partager les maquettes.
```

## Modèle de compte-rendu de fin de session

Utilisez ce schéma à chaque fin de session de travail pour faire le point rapidement.

```text
[DATE] : JJ/MM/AAAA

1) Tâches réalisées aujourd'hui
- [ ] Tâche 1
- [ ] Tâche 2
- [ ] Tâche 3

2) Tâches en cours / à continuer
- [ ] Tâche en cours 1
- [ ] Tâche en cours 2

3) Tâches à faire ensuite
- [ ] Prochaine tâche 1
- [ ] Prochaine tâche 2

4) Blocages / décisions prises
- [ ] Point bloquant 1
- [ ] Décision 1

5) Notes / remarques
- 
```

### Schéma visuel de fin de session

```text
Session start --> Objectifs définis --> Travail réalisé --> Résultats / progrès --> Prochaines actions --> Session close
```

> Astuce : recopier ce modèle dans le fichier de notes ou le ticket de suivi à chaque fin de journée pour garder une trace claire des progrès et des prochaines étapes.
