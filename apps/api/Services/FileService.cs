using Api.Models;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Api.Services;

public class FileService
{
    private readonly SqliteConnection _db;
    private readonly IConfiguration _config;
    private readonly string _uploadPath;

    public FileService(SqliteConnection db, IConfiguration config)
    {
        _db = db;
        _config = config;
        var raw = config["Storage:UploadPath"] ?? "uploads";
        _uploadPath = Path.IsPathRooted(raw)
            ? raw
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, raw));
    }

    public async Task<(ArquivoPdf? arquivo, string? error)> SaveFileAsync(string userId, IFormFile file)
    {
        if (file.ContentType != "application/pdf" &&
            !file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            return (null, "Apenas arquivos PDF são aceitos");

        var maxSizeMb = int.TryParse(_config["Storage:MaxFileSizeMB"], out var mb) ? mb : 10;
        if (file.Length > maxSizeMb * 1024L * 1024L)
            return (null, $"Arquivo muito grande. Máximo: {maxSizeMb}MB");

        var userFolder = Path.Combine(_uploadPath, userId);
        Directory.CreateDirectory(userFolder);

        var arquivoId = Guid.NewGuid().ToString();
        var nomeStorage = $"{arquivoId}.pdf";
        var filePath = Path.Combine(userFolder, nomeStorage);

        await using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        await _db.OpenAsync();

        var arquivo = new ArquivoPdf
        {
            Id = arquivoId,
            UserId = userId,
            NomeOriginal = file.FileName,
            NomeStorage = nomeStorage,
            TamanhoBytes = file.Length,
            Status = "pendente",
            CreatedAt = DateTime.UtcNow
        };

        await _db.ExecuteAsync("""
            INSERT INTO arquivos_pdf (id, user_id, nome_original, nome_storage, tamanho_bytes, status, created_at)
            VALUES (@Id, @UserId, @NomeOriginal, @NomeStorage, @TamanhoBytes, @Status, @CreatedAt)
            """, arquivo);

        return (arquivo, null);
    }

    public async Task<List<ArquivoPdfDto>> GetUserFilesAsync(string userId)
    {
        await _db.OpenAsync();

        var arquivos = (await _db.QueryAsync<ArquivoPdf>(
            "SELECT * FROM arquivos_pdf WHERE user_id = @UserId ORDER BY created_at DESC",
            new { UserId = userId })).ToList();

        if (arquivos.Count == 0) return new List<ArquivoPdfDto>();

        // Carrega holerites em batch (evita N+1)
        var arquivoIds = arquivos.Select(a => a.Id).ToArray();
        var holeritesPorArquivo = (await _db.QueryAsync<(string ArquivoId, int Ano, int Mes, string Tipo)>(@"
            SELECT arquivo_id as ArquivoId, ano as Ano, mes as Mes, tipo as Tipo
            FROM holerites
            WHERE arquivo_id IN @Ids", new { Ids = arquivoIds }))
            .GroupBy(h => h.ArquivoId)
            .ToDictionary(g => g.Key, g => g.ToList());

        return arquivos.Select(arquivo =>
        {
            var dto = new ArquivoPdfDto
            {
                Id = arquivo.Id,
                NomeOriginal = arquivo.NomeOriginal,
                TamanhoBytes = arquivo.TamanhoBytes,
                Status = arquivo.Status,
                ErroMensagem = arquivo.ErroMensagem,
                CreatedAt = arquivo.CreatedAt,
                ProcessedAt = arquivo.ProcessedAt,
                HoleritesCount = holeritesPorArquivo.TryGetValue(arquivo.Id, out var hs) ? hs.Count : 0
            };

            if (holeritesPorArquivo.TryGetValue(arquivo.Id, out var holerites) && holerites.Count > 0)
            {
                var first = holerites[0];
                dto.Ano = first.Ano;
                dto.Mes = first.Mes;
                dto.Tipo = first.Tipo;
                dto.Referencia = $"{first.Mes:00}/{first.Ano}";
            }

            return dto;
        }).ToList();
    }

    public async Task<ArquivoPdf?> GetFileByIdAsync(string userId, string arquivoId)
    {
        await _db.OpenAsync();
        return await _db.QueryFirstOrDefaultAsync<ArquivoPdf>(
            "SELECT * FROM arquivos_pdf WHERE id = @Id AND user_id = @UserId",
            new { Id = arquivoId, UserId = userId });
    }

    public string GetFilePath(string userId, string nomeStorage) =>
        Path.Combine(_uploadPath, userId, nomeStorage);

    public async Task<bool> DeleteFileAsync(string userId, string arquivoId)
    {
        await _db.OpenAsync();

        var arquivo = await GetFileByIdAsync(userId, arquivoId);
        if (arquivo == null) return false;

        var filePath = GetFilePath(userId, arquivo.NomeStorage);
        if (File.Exists(filePath))
            File.Delete(filePath);

        await _db.ExecuteAsync(
            "DELETE FROM arquivos_pdf WHERE id = @Id AND user_id = @UserId",
            new { Id = arquivoId, UserId = userId });

        return true;
    }

    public async Task UpdateStatusAsync(string arquivoId, string status, string? erroMensagem = null)
    {
        await _db.OpenAsync();
        await _db.ExecuteAsync("""
            UPDATE arquivos_pdf
            SET status = @Status,
                erro_mensagem = @ErroMensagem,
                processed_at = CASE WHEN @Status IN ('processado', 'erro') THEN datetime('now') ELSE processed_at END
            WHERE id = @Id
            """, new { Id = arquivoId, Status = status, ErroMensagem = erroMensagem });
    }

    public async Task<int> DeleteAllFilesAsync(string userId)
    {
        await _db.OpenAsync();

        var arquivos = await _db.QueryAsync<ArquivoPdf>(
            "SELECT * FROM arquivos_pdf WHERE user_id = @UserId",
            new { UserId = userId });

        var count = 0;
        foreach (var arquivo in arquivos)
        {
            var filePath = GetFilePath(userId, arquivo.NomeStorage);
            if (File.Exists(filePath))
                File.Delete(filePath);
            count++;
        }

        var userFolder = Path.Combine(_uploadPath, userId);
        if (Directory.Exists(userFolder))
        {
            try { Directory.Delete(userFolder, recursive: false); }
            catch (IOException) { /* pasta não vazia: ignora */ }
        }

        await _db.ExecuteAsync(
            "DELETE FROM arquivos_pdf WHERE user_id = @UserId",
            new { UserId = userId });

        return count;
    }
}
