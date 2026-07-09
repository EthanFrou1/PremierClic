# PremierClic

Personal prospecting tool for local businesses (Perpignan / Pyrénées-Orientales).

## Local development

Requirements: Docker & Docker Compose

1. Copy `.env.example` to `.env` and fill secrets (DB password, `JWT_SECRET`, SMTP credentials).

2. Build and run services:

```bash
docker compose up --build
```

Or use the helper script from the repository root to stop and restart everything in one command:

```powershell
.\refresh.ps1
```

This will start three services:
- `db` Postgres on port `5432`
- `api` ASP.NET Core on port `5000`
- `front` React app on port `3000`

## Repo structure

- `/api` — ASP.NET Core Web API (back-end)
- `/front` — React (Vite) + Tailwind front-end
- `docker-compose.yml` — local compose setup

## Deployment (Coolify)

- Push this repository to GitHub.
- Add the repo to Coolify and choose "Docker Compose" deployment.
- Configure required environment variables in Coolify (DB, JWT, SMTP). Never commit secrets.
- Configure domain and enable automatic Let's Encrypt certificate.

---

Next steps: implement the API (EF Core models, migrations, CRUD), then the front-end views.