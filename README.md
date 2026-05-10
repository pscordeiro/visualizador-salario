# Visualizador de Salário

Self-hosted dashboard for Brazilian payroll PDFs (holerites). Drop your contracheque PDFs, get charts: salary evolution, gross vs net, taxes paid (INSS/IRRF), FGTS accrued, year-over-year breakdown.

**Everything runs and is processed locally on your machine** — payroll data is sensitive, so nothing leaves your computer (no telemetry, no third-party APIs, no cloud).

> 🇧🇷 Read in Portuguese: [README.pt-BR.md](./README.pt-BR.md)

![stack](https://img.shields.io/badge/.NET-10-512BD4) ![stack](https://img.shields.io/badge/Vue-3-42b883) ![stack](https://img.shields.io/badge/Python-3-3776AB) ![stack](https://img.shields.io/badge/SQLite-3-003B57) ![license](https://img.shields.io/badge/license-MIT-green)

## Why
Most people only see the bottom number on payday. With a few PDFs and one `docker compose up`, you get a clear picture of how your compensation evolves, what the government takes, and how vacation/13th salary stack up.

## Features
- Multi-user with JWT (access + refresh token rotation, BCrypt-hashed)
- Drag & drop upload, batch processing, reprocessing
- Auto-detects payroll type: regular monthly, vacation, 13th salary (advance/full), complementary/PLR
- Per-user data isolation (you only ever see your own holerites)
- Dashboards with [Apache ECharts](https://echarts.apache.org/): salary evolution, gross/net, tax distribution, year totals
- Drill-down per holerite with all "rubricas" (line items)
- "Hide values" toggle for screenshots/screen sharing
- Generic parser for common Brazilian payroll layouts

## Quick start (Docker)
```bash
cp .env.example .env
# Edit .env and set JWT_SECRET to a random 32+ char string
# (e.g. openssl rand -base64 48)

docker compose up -d --build
```

- App: http://localhost:8080
- API docs (Scalar): http://localhost:5074/docs
- OpenAPI spec: http://localhost:5074/openapi/v1.json

## Quick start (local dev)
Requires .NET 10 SDK, Node 20+, Python 3.10+.

```bash
# 1) Backend
cd apps/api
dotnet run                    # http://localhost:5074

# 2) Extractor venv (one-time setup)
cd ../extractor
python3 -m venv venv
./venv/bin/pip install -r requirements.txt
# Then point the API to it via env var:
#   APP_Extractor__PythonPath=$(pwd)/venv/bin/python3

# 3) Frontend
cd ../web
npm install
npm run dev                   # http://localhost:5173
```

The DB schema is created automatically on first run (`DatabaseInitializer.cs`).

## Project structure
```
apps/
├── api/         # .NET 10 — Minimal APIs, Dapper, SQLite
├── web/         # Vue 3 + Vite + Tailwind + ECharts
└── extractor/   # Python — pdfplumber-based parser
docker/
├── api.Dockerfile, web.Dockerfile, nginx.conf
docker-compose.yml
docs/ARQUITETURA.md     # deeper architecture notes (pt-BR)
```

## Configuration
All settings can be overridden via env vars with the `APP_` prefix and `__` as section separator:

| Var | Default | Notes |
|---|---|---|
| `APP_Jwt__Secret` | — | **Required**, ≥ 32 chars |
| `APP_Jwt__AccessTokenExpireMinutes` | `30` | |
| `APP_Jwt__RefreshTokenExpireDays` | `7` | |
| `APP_Cors__Origins` | `http://localhost:5173,http://localhost:8080` | comma-separated |
| `APP_Storage__MaxFileSizeMB` | `10` | per file |
| `APP_Database__Path` | `../../database/salarios.db` | resolved from binary dir |

In `docker-compose.yml` these are wired from `.env` automatically.

## Privacy
Everything runs locally. No analytics, no third-party API calls. Your `database/salarios.db` and uploaded PDFs are git-ignored — they never leave your machine unless *you* push them.

## Parser coverage
The Python parser targets common Brazilian payroll layouts. It auto-detects:
- Period via "Competência: MM/YYYY" or "Mês de YYYY" (with filename fallbacks)
- Company via line above CNPJ or "Razão Social:" label
- Position via "Cargo:" / "Função:" / CBO label
- Type (monthly / vacation / 13th / complementary)

If your PDF doesn't parse cleanly, open an issue with a redacted sample.

## Roadmap
- [ ] Async processing queue for large batches
- [ ] Export reports (CSV/PDF)
- [ ] Comparison with INSS/IRRF tables to flag overpayment
- [ ] Multi-language UI (currently pt-BR only)

## License
MIT — see [LICENSE](./LICENSE).
