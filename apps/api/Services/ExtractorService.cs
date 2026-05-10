using System.Diagnostics;

namespace Api.Services;

public class ExtractorService
{
    private readonly IConfiguration _config;
    private readonly FileService _fileService;
    private readonly ILogger<ExtractorService> _logger;

    public ExtractorService(IConfiguration config, FileService fileService, ILogger<ExtractorService> logger)
    {
        _config = config;
        _fileService = fileService;
        _logger = logger;
    }

    public async Task<(bool success, string output, string error)> ProcessFileAsync(
        string userId,
        string arquivoId,
        string filePath)
    {
        var pythonPath = _config["Extractor:PythonPath"] ?? "python3";
        var scriptPath = ResolvePath(_config["Extractor:ScriptPath"] ?? "../extractor/main.py");
        var dbPath = ResolvePath(_config["Database:Path"] ?? "../../database/salarios.db");
        var absoluteFilePath = Path.GetFullPath(filePath);

        await _fileService.UpdateStatusAsync(arquivoId, "processando");

        // Resolve python executable. Se não for um caminho com separador, deixa o SO buscar no PATH.
        var pythonExe = (pythonPath.Contains('/') || pythonPath.Contains('\\'))
            ? Path.GetFullPath(pythonPath)
            : pythonPath;

        var startInfo = new ProcessStartInfo
        {
            FileName = pythonExe,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(scriptPath) ?? "."
        };
        startInfo.ArgumentList.Add(scriptPath);
        startInfo.ArgumentList.Add("--file"); startInfo.ArgumentList.Add(absoluteFilePath);
        startInfo.ArgumentList.Add("--user-id"); startInfo.ArgumentList.Add(userId);
        startInfo.ArgumentList.Add("--arquivo-id"); startInfo.ArgumentList.Add(arquivoId);
        startInfo.ArgumentList.Add("--db"); startInfo.ArgumentList.Add(dbPath);

        _logger.LogInformation("Executando extrator: {Python} {Script}", pythonExe, scriptPath);

        try
        {
            using var process = new Process { StartInfo = startInfo };
            process.Start();

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            var output = await outputTask;
            var error = await errorTask;

            if (process.ExitCode == 0)
            {
                await _fileService.UpdateStatusAsync(arquivoId, "processado");
                return (true, output, "");
            }

            var errorMsg = ExtractFriendlyError(error, output);
            await _fileService.UpdateStatusAsync(arquivoId, "erro", errorMsg);
            _logger.LogWarning("Falha ao processar {ArquivoId}: {Error}", arquivoId, errorMsg);
            return (false, output, errorMsg);
        }
        catch (Exception ex)
        {
            await _fileService.UpdateStatusAsync(arquivoId, "erro", ex.Message);
            _logger.LogError(ex, "Exceção ao processar arquivo {ArquivoId}", arquivoId);
            return (false, "", ex.Message);
        }
    }

    private static string ResolvePath(string path)
    {
        if (Path.IsPathRooted(path)) return path;
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, path));
    }

    private static string ExtractFriendlyError(string stderr, string stdout)
    {
        var combined = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
        // Mensagens "ERRO_DUPLICATA: ..." viram a primeira linha amigável
        foreach (var line in combined.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("ERRO_DUPLICATA:", StringComparison.OrdinalIgnoreCase))
                return trimmed["ERRO_DUPLICATA:".Length..].Trim();
            if (trimmed.StartsWith("ERRO:", StringComparison.OrdinalIgnoreCase))
                return trimmed["ERRO:".Length..].Trim();
        }
        return string.IsNullOrWhiteSpace(combined) ? "Falha desconhecida no processamento" : combined.Trim();
    }
}
