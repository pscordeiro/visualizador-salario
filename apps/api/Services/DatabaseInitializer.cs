using Microsoft.Data.Sqlite;

namespace Api.Services;

/// <summary>Cria o schema do SQLite na primeira execução. Idempotente.</summary>
public static class DatabaseInitializer
{
    public static void Initialize(string dbPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

        using var conn = new SqliteConnection($"Data Source={dbPath}");
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            PRAGMA foreign_keys = ON;

            CREATE TABLE IF NOT EXISTS users (
                id TEXT PRIMARY KEY,
                email TEXT NOT NULL UNIQUE,
                password_hash TEXT NOT NULL,
                nome TEXT,
                created_at TEXT NOT NULL DEFAULT (datetime('now'))
            );
            CREATE INDEX IF NOT EXISTS idx_users_email ON users(email);

            CREATE TABLE IF NOT EXISTS arquivos_pdf (
                id TEXT PRIMARY KEY,
                user_id TEXT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
                nome_original TEXT NOT NULL,
                nome_storage TEXT NOT NULL,
                tamanho_bytes INTEGER NOT NULL,
                status TEXT NOT NULL DEFAULT 'pendente',
                erro_mensagem TEXT,
                created_at TEXT NOT NULL DEFAULT (datetime('now')),
                processed_at TEXT
            );
            CREATE INDEX IF NOT EXISTS idx_arquivos_user_id ON arquivos_pdf(user_id);

            CREATE TABLE IF NOT EXISTS refresh_tokens (
                id TEXT PRIMARY KEY,
                user_id TEXT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
                token_hash TEXT NOT NULL,
                expires_at TEXT NOT NULL,
                created_at TEXT NOT NULL DEFAULT (datetime('now'))
            );
            CREATE INDEX IF NOT EXISTS idx_refresh_tokens_user_id ON refresh_tokens(user_id);

            CREATE TABLE IF NOT EXISTS holerites (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                user_id TEXT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
                arquivo_id TEXT REFERENCES arquivos_pdf(id) ON DELETE CASCADE,
                ano INTEGER NOT NULL,
                mes INTEGER NOT NULL,
                tipo TEXT NOT NULL,
                empresa TEXT,
                cnpj TEXT,
                cargo TEXT,
                salario_base REAL,
                total_vencimentos REAL,
                total_descontos REAL,
                valor_liquido REAL,
                base_inss REAL,
                base_fgts REAL,
                fgts_mes REAL,
                base_irrf REAL,
                faixa_irrf REAL,
                arquivo_origem TEXT,
                UNIQUE(user_id, ano, mes, tipo, arquivo_origem)
            );
            CREATE INDEX IF NOT EXISTS idx_holerites_user_id ON holerites(user_id);
            CREATE INDEX IF NOT EXISTS idx_holerites_arquivo_id ON holerites(arquivo_id);

            CREATE TABLE IF NOT EXISTS rubricas (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                holerite_id INTEGER NOT NULL REFERENCES holerites(id) ON DELETE CASCADE,
                codigo TEXT,
                descricao TEXT,
                referencia TEXT,
                vencimento REAL DEFAULT 0,
                desconto REAL DEFAULT 0
            );
            CREATE INDEX IF NOT EXISTS idx_rubricas_holerite_id ON rubricas(holerite_id);
            """;
        cmd.ExecuteNonQuery();
    }
}
