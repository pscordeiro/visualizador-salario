# Arquitetura

## Visão geral
Aplicação web para análise de holerites brasileiros em PDF. O backend recebe os PDFs, dispara um extrator Python via subprocess, persiste os dados em SQLite e expõe endpoints autenticados (JWT). O frontend consome esses endpoints e renderiza dashboards.

## Stack
| Camada | Tecnologia |
|---|---|
| Frontend | Vue 3, TypeScript, Vite, Tailwind CSS, ECharts |
| Backend | .NET 10, ASP.NET Core Minimal APIs, Dapper, OpenAPI + Scalar (`/docs`) |
| Banco | SQLite |
| Extrator | Python 3, pdfplumber |
| Auth | JWT access token + refresh token rotativo (BCrypt) |

## Estrutura
```
visualizador-salario/
├── apps/
│   ├── api/         # .NET (Program.cs, Services, Models)
│   ├── web/         # Vue (src/, vite.config.ts)
│   └── extractor/   # Python (main.py, parser.py)
├── docker/
│   ├── api.Dockerfile
│   ├── web.Dockerfile
│   └── nginx.conf
├── database/        # salarios.db (gerado em runtime)
├── docs/
├── docker-compose.yml
└── .env.example
```

## Fluxo de upload
1. Usuário faz upload no frontend (`/api/arquivos/upload`).
2. Backend salva o PDF em `uploads/{userId}/{arquivoId}.pdf` e cria registro em `arquivos_pdf` com status `pendente`.
3. Backend invoca `extractor/main.py` via subprocess passando `--file`, `--user-id`, `--arquivo-id`, `--db`.
4. O extrator parseia o PDF (`pdfplumber`), valida duplicata, insere em `holerites` + `rubricas`.
5. Exit codes: `0` sucesso, `1` erro genérico, `2` duplicata. Backend atualiza status do arquivo.
6. Se o parse falhar, o backend remove o PDF físico e o registro para não acumular lixo.

## Modelo de dados
```
users (id, email, password_hash, nome, created_at)
refresh_tokens (id, user_id, token_hash, expires_at, created_at)
arquivos_pdf (id, user_id, nome_original, nome_storage, tamanho_bytes, status, ...)
holerites (id, user_id, arquivo_id, ano, mes, tipo, salario_base, valor_liquido, ...)
rubricas (id, holerite_id, codigo, descricao, vencimento, desconto)
```

`tipo` ∈ `{ MENSAL, FERIAS, 13A, 13I, 14S }`.
Restrição `UNIQUE(user_id, ano, mes, tipo, arquivo_origem)` evita duplicatas de holerite.

O schema é criado automaticamente na primeira execução por `DatabaseInitializer.cs` (idempotente). Não há migrations externas.

## Refresh token
- Formato: `"{id}.{secret}"` — o `id` é o PK e permite lookup O(1) (sem scan + bcrypt em todos os tokens).
- O `secret` é validado via `BCrypt.Verify`.
- Rotação: cada chamada a `/api/auth/refresh` revoga o token usado e emite um novo. Se um atacante reusar o token revogado, a chamada cai em `expired_or_unknown`.

## Configuração
| Variável (env) | Equivalente em config | Default |
|---|---|---|
| `APP_Jwt__Secret` | `Jwt:Secret` | (obrigatório, ≥ 32 chars) |
| `APP_Jwt__Issuer` | `Jwt:Issuer` | `visualizador-salario` |
| `APP_Jwt__Audience` | `Jwt:Audience` | `visualizador-salario-client` |
| `APP_Cors__Origins` | `Cors:Origins` | `http://localhost:5173,http://localhost:8080` |
| `APP_Storage__UploadPath` | `Storage:UploadPath` | `uploads` |
| `APP_Storage__MaxFileSizeMB` | `Storage:MaxFileSizeMB` | `10` |
| `APP_Database__Path` | `Database:Path` | `../../database/salarios.db` |
| `APP_Extractor__PythonPath` | `Extractor:PythonPath` | `python3` |
| `APP_Extractor__ScriptPath` | `Extractor:ScriptPath` | `../extractor/main.py` |

Caminhos relativos são resolvidos a partir de `AppContext.BaseDirectory` (diretório do binário), não do CWD.

## Endpoints principais
### Auth
| Método | Rota | Descrição |
|---|---|---|
| POST | `/api/auth/register` | Cria usuário e devolve par access+refresh |
| POST | `/api/auth/login` | Login (par access+refresh) |
| POST | `/api/auth/refresh` | Rotaciona refresh token |
| POST | `/api/auth/logout` | Revoga refresh token |
| GET | `/api/auth/me` | Dados do usuário autenticado |

### Arquivos
| Método | Rota | Notas |
|---|---|---|
| POST | `/api/arquivos/upload` | Multipart `files[]`. Retorna 200 / 207 (parcial) / 400 (todos falharam) |
| GET | `/api/arquivos` | Lista arquivos do usuário com referência (mês/ano/tipo) e contagem de holerites |
| DELETE | `/api/arquivos/{id}` | Remove arquivo (cascata em holerites/rubricas) |
| DELETE | `/api/arquivos` | Remove todos os arquivos do usuário |
| POST | `/api/arquivos/{id}/reprocessar` | Re-executa o extrator |

### Análises
| Método | Rota |
|---|---|
| GET | `/api/holerites`, `/api/holerites/{ano}`, `/api/holerites/{ano}/{mes}` |
| GET | `/api/rubricas/{holeriteId}` |
| GET | `/api/resumo/anual`, `/api/resumo/impostos`, `/api/impostos/mensal` |
| GET | `/api/evolucao/salario`, `/api/evolucao/liquido` |
| GET | `/api/estatisticas`, `/api/beneficios` |

## Limitações conhecidas
- Processamento é síncrono (subprocess bloqueante). Para uploads em lote grande, considerar uma fila.
- SQLite tem single-writer; ok para uso pessoal/baixa concorrência.
- O parser cobre layouts comuns brasileiros, mas leiautes muito proprietários podem precisar de ajustes em `apps/extractor/parser.py`.
