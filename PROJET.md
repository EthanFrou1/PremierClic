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
- Recherche de prospects avec filtres par statut, catégorie et ville. La liste (`GET /api/prospects`) renvoie aussi `hasMockup` / `hasDeployedMockup` par prospect (calculé côté serveur à partir des `Mockups` liés) pour affichage direct côté front.
- Import automatique de prospects via CSV ou JSON.
- Fonctionnalités de découverte automatique : intégration d'une recherche locale (Overpass / Google Places) et import de commerces.
- Résolution automatique de la fiche Google Place d'un prospect (`place_id`), avec repli sur une recherche par nom/adresse (Find Place From Text) si l'identifiant n'est pas connu.
- Génération automatique d'un **prompt de maquette prêt à coller dans Claude Design** (`GET /api/prospects/discover/google/mockup-prompt/{id}`), construit à partir de la fiche Google Business du prospect :
  - coordonnées, horaires d'ouverture, adresse,
  - avis clients les mieux notés (filtrés ≥ 4 étoiles, tronqués et priorisés par concision),
  - informations pratiques via l'API Google Places (New) : accessibilité, moyens de paiement, stationnement,
  - photos officielles de l'établissement uniquement (comparées au vrai nom de la fiche Google Business, pas au nom saisi côté prospect, pour éviter les faux négatifs),
  - liens photo ajoutés manuellement (`ProspectPhotoLink`) et note indiquant que d'autres images seront collées directement dans la conversation Claude Design,
  - consignes intégrées au prompt pour un rendu mobile-first, sans bug de scroll, et avec un bandeau « aperçu de maquette » sous le header (voir plus bas).
- **Liens photo par prospect** (`ProspectPhotoLinksController`, modèle `ProspectPhotoLink`) : ajout/suppression d'URLs d'images directes (`GET/POST/DELETE /api/prospects/{id}/prospectphotolinks`), typiquement récupérées via Inspecter sur une photo Google Maps. Remplace l'ancien système d'upload de fichiers (`ProspectPhoto`, supprimé), Claude Design ne pouvant pas exploiter des URLs `localhost`.
- **Déploiement automatique des maquettes sur Netlify depuis l'app** :
  - `POST /api/prospects/{id}/mockups/deploy` : upload direct d'un export HTML, zip + déploiement Netlify en une étape.
  - `POST /api/prospects/{id}/mockups/{mockupId}/deploy` : (re)déploie une maquette déjà uploadée côté serveur (fichier stocké via « Ajouter maquette »), sans devoir resélectionner le fichier dans le navigateur.
  - Le zip inclut désormais un fichier `_headers` Netlify forçant `Content-Type: text/html; charset=UTF-8` (la méthode de déploiement par zip brut de Netlify sert sinon les pages en `text/plain`, non rendues par le navigateur).
  - Un script est injecté automatiquement dans le HTML avant déploiement pour afficher un bandeau « Aperçu de maquette — site pas encore en ligne » juste sous le `<header>` détecté (avec retries sur quelques secondes pour les pages qui s'assemblent en JS), afin d'éviter qu'un commerçant ne croie que son site est déjà publié.
  - Le lien de déploiement obtenu est enregistré automatiquement comme `Mockup` (`UrlPreview`).
- **Message de prospection** (remplace l'ancien flux d'email avec placeholders/désinscription, jugé peu utile car l'email du commerçant est rarement disponible) :
  - `GET /api/prospects/discover/google/email-prompt/{id}?canal=Email|Instagram|WhatsApp|Autre` : génère un prompt prêt à coller dans Claude, adapté au canal choisi (email formel avec Objet/Corps vs message court et décontracté pour réseaux sociaux), incluant les infos du commerce, des avis représentatifs, le lien de la maquette déployée si disponible, le lien du site de l'entreprise (`COMPANY_WEBSITE_URL`, en preuve sociale) et le lien de la grille tarifaire si configuré (`PRICING_PAGE_URL`).
  - `ProspectMessagesController` (modèle `ProspectMessage`) : sauvegarde du prompt + du message final (collé après génération par Claude) + canal choisi, par prospect (`GET/POST/DELETE /api/prospects/{id}/prospectmessages`), pour retrouver et copier le message au moment de l'envoyer manuellement (Instagram, WhatsApp, etc.).
- Ancien système d'emails avec templates/placeholders (`EmailController`, `EmailTemplate`, `EmailEnvoye`, désinscription) conservé côté backend mais retiré du parcours utilisateur front (plus utilisé pour l'instant).

### Front-end (`/front`)
- Page de connexion sécurisée.
- Interface prospects :
  - liste des prospects avec **ligne entièrement cliquable** vers la fiche détail (plus de bouton « Voir »), lien Google Business et bouton « Rafraîchir » isolés du clic de ligne,
  - colonne « Maquette » indiquant l'état (—, Ajoutée, Déployée) directement dans la liste,
  - **filtres en listes déroulantes** (Statut, Catégorie, Ville — options dynamiques selon les prospects existants —, Maquette) appliqués automatiquement au changement, sans bouton « Filtrer »,
  - formulaire de création de prospect avec autocomplétion de ville via le widget natif `google.maps.places.Autocomplete`,
  - découverte automatique de prospects locaux.
- Page de détail prospect (`/prospects/:id`) :
  - section « Prompt maquette » pour générer et copier en un clic le prompt Claude Design du prospect,
  - section « Liens photos » pour coller des URLs d'images directes, injectées dans le prompt de maquette,
  - section « Maquettes » : un seul champ d'upload (image ou export HTML), bouton « Ajouter maquette », puis bouton « Déployer sur Netlify »/« Redéployer sur Netlify » directement sur chaque maquette de la liste,
  - section « Message de prospection » : sélecteur de canal (Email / Instagram / WhatsApp / Autre) → génération du prompt adapté → collage du message généré par Claude → sauvegarde, avec historique des messages enregistrés (copie / suppression).
- Gestion des états de chargement et des erreurs.
- Navigation basique entre les écrans.

### Outils annexes
- Script `deploy-maquette.ps1` (racine du repo) : déploie une maquette HTML (export Claude Design) sur Netlify en une seule commande et copie automatiquement le lien public partageable dans le presse-papiers. Conservé comme solution de secours en ligne de commande.
- Site Netlify dédié `premierclic-maquettes` (id `0cd04bcc-b7e3-4225-a0ba-45a1c9b94b9a`) utilisé comme cible de déploiement par le bouton in-app et par le script. Attention : site **partagé entre tous les prospects** — toujours utiliser le lien avec préfixe d'ID de déploiement (`https://<deploy-id>--premierclic-maquettes.netlify.app`), qui reste stable, plutôt que l'URL racine du site qui pointe vers le dernier déploiement en date.

### Données et modèles
- Modèles métier existants : Prospect, User, EmailTemplate, EmailEnvoye, Mockup, ProspectPhotoLink, ProspectMessage.
- Migrations EF Core pour initialiser la base de données.

## Points forts actuels

- Architecture full-stack claire entre `api` et `front`.
- Déploiement local reproductible via Docker Compose.
- Fonctionnalités de base de prospection opérationnelles.
- Import et découverte automatique déjà disponibles.
- Chaîne de prospection quasi automatisée de bout en bout : découverte → enrichissement Google → prompt de maquette (+ liens photo) → maquette déployée sur Netlify en un clic (avec bandeau « aperçu ») → prompt de message de prospection adapté au canal (email/Instagram/WhatsApp) → message sauvegardé prêt à copier, avec un minimum de saisie manuelle.

## Idées et améliorations à faire plus tard

### Fonctionnalités métiers
- Ajouter la suppression et la mise à jour complète de prospects côté front.
- Ajouter des fiches clients avec historique de contact.
- Ajouter des catégories métiers et des labels personnalisés.
- Ajouter un tableau de bord avec statistiques de conversion.
- Ajouter un module de suivi des relances et des actions commerciales.
- ~~Enregistrer automatiquement le lien de la maquette déployée (Netlify) dans la fiche prospect~~ → fait (bouton « Déployer sur Netlify » in-app).
- ~~Permettre la suppression d'un Mockup depuis la fiche prospect~~ → toujours pas fait pour les Mockups (seulement pour ProspectPhotoLink et ProspectMessage) ; à ajouter si les maquettes ratées/obsolètes s'accumulent.
- ~~Créer une page de tarifs statique (HTML stylée, à la manière d'un exemple déjà réalisé pour un client) et renseigner son URL dans `PRICING_PAGE_URL` pour qu'elle soit intégrée automatiquement aux prompts de message.~~ → fait (page `tarifs.html` sur `website-etnof-web`, `PRICING_PAGE_URL` renseigné).
- Marquer un message de prospection comme « envoyé » (au-delà du simple enregistrement) pour un vrai suivi CRM par canal.
- Nettoyer/décommissionner l'ancien système d'email par templates (`EmailController`, `EmailTemplate`, `EmailEnvoye`, désinscription) s'il reste durablement inutilisé, ou le relier au nouveau système de messages si l'email redevient pertinent.

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
- ~~Ajouter un fichier `.env.example` complet avec toutes les variables attendues~~ → fait (variables Netlify + liens de prospection ajoutés ; une vraie clé Google Maps qui avait été committée par erreur dans `.env.example` a aussi été retirée et remplacée par un placeholder — `.env` reste correctement gitignoré).
- Créer une documentation de déploiement pour Coolify / Docker.
- Ajouter un script de backup de la base de données.
- Mettre en place un pipeline CI/CD pour tests et déploiement.

## Journal des sessions

```text
[DATE] : 09/07/2026 (session 2)

1) Tâches réalisées aujourd'hui
- [x] Création de la page de tarifs statique `tarifs.html` dans le repo `website-etnof-web`, sur la base de la charte graphique du site (variables CSS, Manrope, boutons pilule, cartes arrondies) — 3 formules provisoires (Essentiel / Pro / Sur-mesure)
- [x] Nouvelles classes CSS dédiées (`pricing-grid`, `pricing-card`, `pricing-card-featured`, etc.) ajoutées à `style.css` du site, responsive via le breakpoint existant
- [x] Lien « Tarifs » ajouté dans la nav principale, le footer (bouton + liens légaux) et `sitemap.xml` du site `website-etnof-web`
- [x] Branche `feat/page-tarifs` poussée sur GitHub (PR à créer manuellement, `gh` CLI absent en local — lien direct fourni)
- [x] `PRICING_PAGE_URL` renseigné dans le `.env` de PremierClic (`https://website-etnof-web.vercel.app/tarifs.html`) ; le câblage backend (`ProspectDiscoveryController`, `docker-compose.yml`) existait déjà et intègre désormais automatiquement ce lien dans le prompt de message de prospection
- [x] Conteneur `api` recréé (`docker compose up -d api`) pour prendre en compte la nouvelle variable

2) Tâches en cours / à continuer
- [ ] Créer la pull request sur GitHub pour la branche `feat/page-tarifs` (lien généré : https://github.com/EthanFrou1/website-etnof-web/pull/new/feat/page-tarifs) puis merger
- [ ] Revoir le contenu de la page tarifs (noms de formules, prix, prestations incluses) — actuellement du placeholder au hasard
- [ ] Vérifier le rendu de `tarifs.html` une fois déployé sur Vercel (desktop + mobile)

3) Tâches à faire ensuite
- [ ] Vérifier en conditions réelles qu'un prompt de message de prospection généré inclut bien le lien de la page tarifs
- [ ] Documentation de déploiement Coolify / Docker
- [ ] Suppression d'un Mockup depuis la fiche prospect

4) Blocages / décisions prises
- [x] Décision : contenu de la page tarifs (noms/prix) volontairement provisoire, à retravailler plus tard une fois la grille tarifaire réelle définie
- [ ] Blocage mineur : `gh` CLI non installé sur la machine, la PR ne peut pas être créée en ligne de commande — création manuelle via le lien GitHub fourni

5) Notes / remarques
- Le lien de la page tarifs n'est utile dans les prompts qu'une fois la PR `feat/page-tarifs` mergée et déployée sur Vercel (sinon `tarifs.html` répond en 404).
```

```text
[DATE] : 09/07/2026

1) Tâches réalisées aujourd'hui
- [x] Remplacement des « Photos personnalisées » (upload de fichiers, inutilisable par Claude Design en local) par des « Liens photos » (`ProspectPhotoLink`, URLs directes récupérées via Inspecter sur Google Maps) — migrations `RemoveProspectPhotos` puis `AddProspectPhotoLinks`
- [x] Correctif du matching des photos officielles Google (comparaison au vrai nom de la fiche Google Business au lieu du nom du prospect en base, qui pouvait différer)
- [x] Fusion des champs d'upload de maquette (un seul champ image/HTML), suppression du bouton général « Déployer sur Netlify », déploiement désormais déclenché par maquette depuis la liste (`POST /api/prospects/{id}/mockups/{mockupId}/deploy`), avec support du redéploiement
- [x] Correctif Netlify : les déploiements par zip brut étaient servis en `Content-Type: text/plain` (page affichée en texte brut au lieu d'être rendue) — ajout d'un fichier `_headers` dans le zip forçant `text/html`
- [x] Correctif d'un bug `ZipArchive` (entrée suivante créée avant fermeture de la précédente) qui faisait planter le redéploiement
- [x] Bandeau automatique « Aperçu de maquette — site pas encore en ligne » injecté sous le header de chaque maquette déployée (script avec retry, résistant aux pages qui s'assemblent en JS), + consigne équivalente ajoutée au prompt Claude Design
- [x] Liste des prospects : colonne « Maquette » (état ajoutée/déployée/aucune), ligne entièrement cliquable vers la fiche détail, filtres passés en listes déroulantes (statut, catégorie, ville, maquette) appliqués automatiquement sans bouton « Filtrer »
- [x] Suppression de l'ancien flux « Email de prospection » (objet/corps, placeholders, désinscription) jugé peu utile en pratique (email du commerçant rarement disponible)
- [x] Nouveau système « Message de prospection » : prompt généré par canal (Email / Instagram / WhatsApp / Autre, formulation adaptée), sauvegarde du prompt + message final par prospect (`ProspectMessage`, migration `AddProspectMessages`), historique consultable/copiable/supprimable
- [x] Intégration du lien du site de l'entreprise (`COMPANY_WEBSITE_URL`) comme preuve sociale dans le prompt de message, et d'un emplacement pour une future page de tarifs (`PRICING_PAGE_URL`, à renseigner une fois créée)

2) Tâches en cours / à continuer
- [ ] Créer la page de tarifs statique (HTML stylée) et renseigner `PRICING_PAGE_URL`
- [ ] Vérifier en conditions réelles l'envoi de messages de prospection générés (Instagram/WhatsApp) auprès de vrais commerçants

3) Tâches à faire ensuite
- [ ] Documentation de déploiement Coolify / Docker (nom de domaine public pour l'API)
- [ ] Suppression d'un Mockup depuis la fiche prospect
- [ ] Décider du sort de l'ancien système d'email par templates (`EmailController`/`EmailTemplate`/`EmailEnvoye`), inutilisé côté front depuis aujourd'hui

4) Blocages / décisions prises
- [x] Décision : abandonner l'email de prospection au profit de messages Instagram/WhatsApp, l'email du commerçant étant rarement disponible via Google Places
- [x] Décision : garder le site Netlify `premierclic-maquettes` partagé entre tous les prospects, en s'appuyant sur les liens de déploiement individuels (préfixés par l'ID de déploiement) plutôt que sur l'URL racine du site, pour que chaque maquette conserve un lien stable
- [x] Décision : la page de tarifs sera une page statique stylée (Canva/HTML), pas un générateur de devis dynamique dans l'app — trop de complexité pour un besoin pas encore validé

5) Notes / remarques
- Le bandeau « aperçu de maquette » est injecté à chaque déploiement/redéploiement ; les maquettes déjà déployées avant ce correctif doivent être redéployées pour en bénéficier.
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
