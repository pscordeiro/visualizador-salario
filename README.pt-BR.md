# Visualizador de Salário

Dashboard self-hosted para visualizar e analisar holerites brasileiros em PDF. Faça upload dos seus contracheques e veja gráficos: evolução salarial, bruto vs líquido, impostos pagos (INSS/IRRF), FGTS acumulado, totais por ano.

**Roda e processa tudo localmente na sua máquina** — holerite tem dado sensível (salário, CPF, CNPJ do empregador), então nada sai do seu computador (sem telemetria, sem chamadas a APIs externas, sem cloud).

> 🇺🇸 English: [README.md](./README.md)

![stack](https://img.shields.io/badge/.NET-10-512BD4) ![stack](https://img.shields.io/badge/Vue-3-42b883) ![stack](https://img.shields.io/badge/Python-3-3776AB) ![stack](https://img.shields.io/badge/SQLite-3-003B57) ![license](https://img.shields.io/badge/licença-MIT-green)

## Motivação
A maioria das pessoas só vê o número final do salário. Com alguns PDFs e um `docker compose up`, você passa a enxergar como sua remuneração evolui, quanto o governo tira e como férias/13º se somam ao total.

## Funcionalidades
- Multi-usuário com JWT (access + refresh token com rotação, senhas com BCrypt)
- Upload com drag & drop, processamento em lote, reprocessamento
- Detecta automaticamente o tipo de folha: mensal, férias, 13º (adiantamento/integral), complementar/PLR
- Isolamento de dados por usuário (você só vê seus próprios holerites)
- Dashboards em [Apache ECharts](https://echarts.apache.org/): evolução salarial, bruto/líquido, distribuição de impostos, totais por ano
- Drill-down por holerite com todas as rubricas
- Botão para ocultar valores (útil para screenshots / compartilhar tela)
- Parser genérico para layouts comuns de holerite brasileiro

## Início rápido (Docker)
```bash
cp .env.example .env
# Edite .env e defina JWT_SECRET com uma string aleatória de 32+ chars
# (exemplo: openssl rand -base64 48)

docker compose up -d --build
```

- App: http://localhost:8080
- Docs da API (Scalar): http://localhost:5074/docs
- Especificação OpenAPI: http://localhost:5074/openapi/v1.json

## Início rápido (dev local)
Requer .NET 10 SDK, Node 20+, Python 3.10+.

```bash
# 1) Backend
cd apps/api
dotnet run                    # http://localhost:5074

# 2) Extractor venv (setup uma vez)
cd ../extractor
python3 -m venv venv
./venv/bin/pip install -r requirements.txt
# Aponte a API para o python da venv via env var:
#   APP_Extractor__PythonPath=$(pwd)/venv/bin/python3

# 3) Frontend
cd ../web
npm install
npm run dev                   # http://localhost:5173
```

O schema do banco é criado automaticamente no primeiro `dotnet run` (`DatabaseInitializer.cs`).

## Estrutura
```
apps/
├── api/         # .NET 10 — Minimal APIs, Dapper, SQLite
├── web/         # Vue 3 + Vite + Tailwind + ECharts
└── extractor/   # Python — parser baseado em pdfplumber
docker/
├── api.Dockerfile, web.Dockerfile, nginx.conf
docker-compose.yml
docs/ARQUITETURA.md     # arquitetura detalhada
```

## Configuração
Tudo pode ser sobrescrito via variável de ambiente com prefixo `APP_` e `__` como separador de seção:

| Variável | Default | Observação |
|---|---|---|
| `APP_Jwt__Secret` | — | **Obrigatório**, ≥ 32 chars |
| `APP_Jwt__AccessTokenExpireMinutes` | `30` | |
| `APP_Jwt__RefreshTokenExpireDays` | `7` | |
| `APP_Cors__Origins` | `http://localhost:5173,http://localhost:8080` | separados por vírgula |
| `APP_Storage__MaxFileSizeMB` | `10` | por arquivo |
| `APP_Database__Path` | `../../database/salarios.db` | resolvido a partir do binário |

No `docker-compose.yml` essas variáveis são preenchidas a partir do `.env` automaticamente.

## Privacidade
Tudo roda localmente. Sem analytics, sem chamadas a APIs de terceiros. O arquivo `database/salarios.db` e os PDFs em `apps/api/uploads/` estão no `.gitignore` — não saem da sua máquina a menos que *você* faça commit/push.

## Cobertura do parser
O parser Python foi feito para layouts comuns de holerite no Brasil. Detecta automaticamente:
- Período via "Competência: MM/YYYY" ou "Mês de YYYY" (com fallback no nome do arquivo)
- Empresa via linha acima do CNPJ ou label "Razão Social:"
- Cargo via labels "Cargo:" / "Função:" / "CBO"
- Tipo (mensal / férias / 13º / complementar)

Se o seu PDF não for parseado corretamente, abra uma issue com um exemplo redigido.

## Roadmap
- [ ] Fila de processamento assíncrono para uploads grandes
- [ ] Exportar relatórios (CSV/PDF)
- [ ] Comparar com tabelas oficiais de INSS/IRRF e sinalizar inconsistências
- [ ] Internacionalização da interface

## Licença
MIT — veja [LICENSE](./LICENSE).
