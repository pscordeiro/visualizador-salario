using System.Security.Claims;
using System.Text;
using Api.Models;
using Api.Services;
using Dapper;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Data.Sqlite;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Permite override via env vars com prefixo APP_ (ex: APP_Jwt__Secret=xxx)
builder.Configuration.AddEnvironmentVariables(prefix: "APP_");

// JWT secret obrigatório
var jwtSecret = builder.Configuration["Jwt:Secret"];
if (string.IsNullOrWhiteSpace(jwtSecret) || jwtSecret.Length < 32)
{
    throw new InvalidOperationException(
        "Jwt:Secret não configurado ou muito curto (mínimo 32 chars). " +
        "Defina via appsettings ou variável de ambiente APP_Jwt__Secret.");
}

// CORS configurável
var corsOrigins = (builder.Configuration["Cors:Origins"] ?? "")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (corsOrigins.Length == 0)
        {
            policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
        }
        else
        {
            policy.WithOrigins(corsOrigins).AllowAnyMethod().AllowAnyHeader();
        }
    });
});

builder.Services.AddOpenApi("v1", options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info.Title = "Visualizador de Salário API";
        document.Info.Version = "v1";
        document.Info.Description = "API para upload de holerites em PDF, extração de dados e análises financeiras.";
        return Task.CompletedTask;
    });
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
        };
    });

builder.Services.AddAuthorization();

// Caminho do DB resolvido a partir do diretório do binário
var dbPath = ResolvePath(builder.Configuration["Database:Path"] ?? "../../database/salarios.db");
Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

builder.Services.AddScoped(_ => new SqliteConnection($"Data Source={dbPath}"));
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<FileService>();
builder.Services.AddScoped<ExtractorService>();

Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;

var app = builder.Build();

// Bootstrap do schema (idempotente)
DatabaseInitializer.Initialize(dbPath);

// Pasta de uploads
var uploadPath = ResolvePath(builder.Configuration["Storage:UploadPath"] ?? "uploads");
Directory.CreateDirectory(uploadPath);

// OpenAPI sempre disponível em /openapi/v1.json; UI do Scalar em /docs
app.MapOpenApi();
app.MapScalarApiReference("/docs", options =>
{
    options
        .WithTitle("Visualizador de Salário")
        .WithTheme(ScalarTheme.Saturn)
        .WithDefaultHttpClient(ScalarTarget.JavaScript, ScalarClient.Fetch);
});

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

string? GetUserId(HttpContext context) =>
    context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

// =====================================================
// AUTENTICAÇÃO
// =====================================================

app.MapPost("/api/auth/register", async (RegisterRequest request, AuthService authService) =>
{
    if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Senha))
        return Results.BadRequest(new { error = "Email e senha são obrigatórios" });

    if (request.Senha.Length < 6)
        return Results.BadRequest(new { error = "Senha deve ter no mínimo 6 caracteres" });

    var (user, error) = await authService.RegisterAsync(request);
    if (error != null) return Results.BadRequest(new { error });

    var (refreshToken, _) = await authService.GenerateRefreshTokenAsync(user!);
    return Results.Ok(new AuthResponse
    {
        AccessToken = authService.GenerateAccessToken(user!),
        RefreshToken = refreshToken,
        User = AuthService.ToDto(user!)
    });
}).WithTags("Auth");

app.MapPost("/api/auth/login", async (LoginRequest request, AuthService authService) =>
{
    if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Senha))
        return Results.BadRequest(new { error = "Email e senha são obrigatórios" });

    var (user, error) = await authService.LoginAsync(request);
    if (error != null) return Results.BadRequest(new { error });

    var (refreshToken, _) = await authService.GenerateRefreshTokenAsync(user!);
    return Results.Ok(new AuthResponse
    {
        AccessToken = authService.GenerateAccessToken(user!),
        RefreshToken = refreshToken,
        User = AuthService.ToDto(user!)
    });
}).WithTags("Auth");

app.MapPost("/api/auth/refresh", async (RefreshRequest request, AuthService authService) =>
{
    if (string.IsNullOrWhiteSpace(request.RefreshToken))
        return Results.BadRequest(new { error = "Refresh token obrigatório" });

    var (user, newRefresh, error) = await authService.RotateRefreshTokenAsync(request.RefreshToken);
    if (error != null) return Results.Unauthorized();

    return Results.Ok(new AuthResponse
    {
        AccessToken = authService.GenerateAccessToken(user!),
        RefreshToken = newRefresh!,
        User = AuthService.ToDto(user!)
    });
}).WithTags("Auth");

app.MapPost("/api/auth/logout", async (RefreshRequest request, AuthService authService) =>
{
    if (!string.IsNullOrWhiteSpace(request.RefreshToken))
        await authService.RevokeRefreshTokenAsync(request.RefreshToken);
    return Results.Ok(new { message = "Sessão encerrada" });
}).WithTags("Auth");

app.MapGet("/api/auth/me", async (HttpContext context, AuthService authService) =>
{
    var userId = GetUserId(context);
    if (userId == null) return Results.Unauthorized();

    var user = await authService.GetUserByIdAsync(userId);
    return user == null ? Results.NotFound() : Results.Ok(AuthService.ToDto(user));
}).RequireAuthorization().WithTags("Auth");

// =====================================================
// ARQUIVOS
// =====================================================

app.MapPost("/api/arquivos/upload", async (
    HttpContext context,
    IFormFileCollection files,
    FileService fileService,
    ExtractorService extractorService) =>
{
    var userId = GetUserId(context);
    if (userId == null) return Results.Unauthorized();
    if (files.Count == 0) return Results.BadRequest(new { error = "Nenhum arquivo enviado" });

    var resultados = new List<object>();
    var sucessos = 0;

    foreach (var file in files)
    {
        var (arquivo, error) = await fileService.SaveFileAsync(userId, file);
        if (error != null)
        {
            resultados.Add(new { arquivo = file.FileName, sucesso = false, erro = error });
            continue;
        }

        var filePath = fileService.GetFilePath(userId, arquivo!.NomeStorage);
        var (success, _, processError) = await extractorService.ProcessFileAsync(userId, arquivo.Id, filePath);

        if (!success)
        {
            // Falha no parse: remove arquivo+registro para não acumular lixo
            await fileService.DeleteFileAsync(userId, arquivo.Id);
        }
        else
        {
            sucessos++;
        }

        resultados.Add(new
        {
            arquivo = file.FileName,
            arquivoId = arquivo.Id,
            sucesso = success,
            erro = success ? null : processError
        });
    }

    if (sucessos == 0)
        return Results.BadRequest(new { resultados });
    if (sucessos < files.Count)
        return Results.Json(new { resultados }, statusCode: StatusCodes.Status207MultiStatus);
    return Results.Ok(new { resultados });
})
.DisableAntiforgery()
.RequireAuthorization()
.WithTags("Arquivos");

app.MapGet("/api/arquivos", async (HttpContext context, FileService fileService) =>
{
    var userId = GetUserId(context);
    if (userId == null) return Results.Unauthorized();
    return Results.Ok(await fileService.GetUserFilesAsync(userId));
}).RequireAuthorization().WithTags("Arquivos");

app.MapGet("/api/arquivos/{id}", async (string id, HttpContext context, FileService fileService) =>
{
    var userId = GetUserId(context);
    if (userId == null) return Results.Unauthorized();

    var arquivo = await fileService.GetFileByIdAsync(userId, id);
    return arquivo == null ? Results.NotFound() : Results.Ok(arquivo);
}).RequireAuthorization().WithTags("Arquivos");

app.MapDelete("/api/arquivos/{id}", async (string id, HttpContext context, FileService fileService) =>
{
    var userId = GetUserId(context);
    if (userId == null) return Results.Unauthorized();

    var deleted = await fileService.DeleteFileAsync(userId, id);
    return deleted ? Results.Ok(new { message = "Arquivo deletado" }) : Results.NotFound();
}).RequireAuthorization().WithTags("Arquivos");

app.MapPost("/api/arquivos/{id}/reprocessar", async (
    string id,
    HttpContext context,
    FileService fileService,
    ExtractorService extractorService,
    SqliteConnection db) =>
{
    var userId = GetUserId(context);
    if (userId == null) return Results.Unauthorized();

    var arquivo = await fileService.GetFileByIdAsync(userId, id);
    if (arquivo == null) return Results.NotFound();

    await db.OpenAsync();
    await db.ExecuteAsync(
        "DELETE FROM holerites WHERE arquivo_id = @ArquivoId AND user_id = @UserId",
        new { ArquivoId = id, UserId = userId });

    var filePath = fileService.GetFilePath(userId, arquivo.NomeStorage);
    var (success, _, error) = await extractorService.ProcessFileAsync(userId, id, filePath);

    return Results.Ok(new { sucesso = success, erro = success ? null : error });
}).RequireAuthorization().WithTags("Arquivos");

app.MapDelete("/api/arquivos", async (HttpContext context, FileService fileService) =>
{
    var userId = GetUserId(context);
    if (userId == null) return Results.Unauthorized();

    var count = await fileService.DeleteAllFilesAsync(userId);
    return Results.Ok(new { message = $"{count} arquivo(s) deletado(s)", count });
}).RequireAuthorization().WithTags("Arquivos");

// =====================================================
// HOLERITES E ANÁLISES
// =====================================================

app.MapGet("/api/holerites", async (HttpContext context, SqliteConnection db) =>
{
    var userId = GetUserId(context);
    if (userId == null) return Results.Unauthorized();

    await db.OpenAsync();
    var holerites = await db.QueryAsync<Holerite>(
        "SELECT * FROM holerites WHERE user_id = @UserId ORDER BY ano, mes, tipo",
        new { UserId = userId });
    return Results.Ok(holerites);
}).RequireAuthorization().WithTags("Holerites");

app.MapGet("/api/holerites/{ano:int}", async (int ano, HttpContext context, SqliteConnection db) =>
{
    var userId = GetUserId(context);
    if (userId == null) return Results.Unauthorized();

    await db.OpenAsync();
    var holerites = await db.QueryAsync<Holerite>(
        "SELECT * FROM holerites WHERE user_id = @UserId AND ano = @ano ORDER BY mes, tipo",
        new { UserId = userId, ano });
    return Results.Ok(holerites);
}).RequireAuthorization().WithTags("Holerites");

app.MapGet("/api/holerites/{ano:int}/{mes:int}", async (int ano, int mes, HttpContext context, SqliteConnection db) =>
{
    var userId = GetUserId(context);
    if (userId == null) return Results.Unauthorized();

    await db.OpenAsync();
    var holerites = await db.QueryAsync<Holerite>(
        "SELECT * FROM holerites WHERE user_id = @UserId AND ano = @ano AND mes = @mes ORDER BY tipo",
        new { UserId = userId, ano, mes });
    return Results.Ok(holerites);
}).RequireAuthorization().WithTags("Holerites");

app.MapGet("/api/rubricas/{holeriteId:int}", async (int holeriteId, HttpContext context, SqliteConnection db) =>
{
    var userId = GetUserId(context);
    if (userId == null) return Results.Unauthorized();

    await db.OpenAsync();
    var ownerExists = await db.ExecuteScalarAsync<long>(
        "SELECT COUNT(*) FROM holerites WHERE id = @holeriteId AND user_id = @UserId",
        new { holeriteId, UserId = userId });
    if (ownerExists == 0) return Results.NotFound();

    var rubricas = await db.QueryAsync<Rubrica>(
        "SELECT * FROM rubricas WHERE holerite_id = @holeriteId",
        new { holeriteId });
    return Results.Ok(rubricas);
}).RequireAuthorization().WithTags("Holerites");

app.MapGet("/api/resumo/anual", async (HttpContext context, SqliteConnection db) =>
{
    var userId = GetUserId(context);
    if (userId == null) return Results.Unauthorized();

    await db.OpenAsync();
    var resumo = await db.QueryAsync<ResumoAnual>(@"
        SELECT
            ano as Ano,
            CAST(SUM(CASE WHEN tipo = 'MENSAL' THEN valor_liquido ELSE 0.0 END) AS REAL) as TotalLiquidoMensal,
            CAST(SUM(CASE WHEN tipo IN ('13A', '13I') THEN valor_liquido ELSE 0.0 END) AS REAL) as Total13,
            CAST(SUM(CASE WHEN tipo = 'FERIAS' THEN valor_liquido ELSE 0.0 END) AS REAL) as TotalFerias,
            CAST(SUM(CASE WHEN tipo = '14S' THEN valor_liquido ELSE 0.0 END) AS REAL) as Total14,
            CAST(SUM(valor_liquido) AS REAL) as TotalGeral,
            CAST(SUM(total_descontos) AS REAL) as TotalDescontos,
            CAST(SUM(fgts_mes) AS REAL) as TotalFgts
        FROM holerites
        WHERE user_id = @UserId
        GROUP BY ano
        ORDER BY ano", new { UserId = userId });
    return Results.Ok(resumo);
}).RequireAuthorization().WithTags("Resumos");

app.MapGet("/api/resumo/impostos", async (HttpContext context, SqliteConnection db) =>
{
    var userId = GetUserId(context);
    if (userId == null) return Results.Unauthorized();

    await db.OpenAsync();
    var impostos = await db.QueryAsync<ResumoImpostos>(@"
        SELECT
            h.ano as Ano,
            SUM(COALESCE(r_inss.desconto, 0)) as TotalInss,
            SUM(COALESCE(r_irrf.desconto, 0)) as TotalIrrf,
            SUM(COALESCE(r_inss.desconto, 0) + COALESCE(r_irrf.desconto, 0)) as TotalImpostos
        FROM holerites h
        LEFT JOIN rubricas r_inss ON r_inss.holerite_id = h.id AND r_inss.codigo = '998'
        LEFT JOIN rubricas r_irrf ON r_irrf.holerite_id = h.id AND r_irrf.codigo = '999'
        WHERE h.user_id = @UserId
        GROUP BY h.ano
        ORDER BY h.ano", new { UserId = userId });
    return Results.Ok(impostos);
}).RequireAuthorization().WithTags("Resumos");

app.MapGet("/api/impostos/mensal", async (HttpContext context, SqliteConnection db) =>
{
    var userId = GetUserId(context);
    if (userId == null) return Results.Unauthorized();

    await db.OpenAsync();
    var impostos = await db.QueryAsync<ImpostoMensal>(@"
        SELECT
            h.ano as Ano,
            h.mes as Mes,
            h.tipo as Tipo,
            printf('%02d/%d', h.mes, h.ano) as Periodo,
            CAST(COALESCE(r_inss.desconto, 0) AS REAL) as Inss,
            CAST(COALESCE(r_irrf.desconto, 0) AS REAL) as Irrf,
            CAST(COALESCE(r_inss.desconto, 0) + COALESCE(r_irrf.desconto, 0) AS REAL) as Total,
            CAST(h.total_vencimentos AS REAL) as Bruto,
            CAST(h.valor_liquido AS REAL) as Liquido
        FROM holerites h
        LEFT JOIN rubricas r_inss ON r_inss.holerite_id = h.id AND r_inss.codigo = '998'
        LEFT JOIN rubricas r_irrf ON r_irrf.holerite_id = h.id AND r_irrf.codigo = '999'
        WHERE h.user_id = @UserId
        ORDER BY h.ano, h.mes,
            CASE h.tipo
                WHEN 'MENSAL' THEN 1
                WHEN 'FERIAS' THEN 2
                WHEN '13A' THEN 3
                WHEN '13I' THEN 4
                WHEN '14S' THEN 5
                ELSE 6
            END", new { UserId = userId });
    return Results.Ok(impostos);
}).RequireAuthorization().WithTags("Resumos");

app.MapGet("/api/evolucao/salario", async (HttpContext context, SqliteConnection db) =>
{
    var userId = GetUserId(context);
    if (userId == null) return Results.Unauthorized();

    await db.OpenAsync();
    var evolucao = await db.QueryAsync<EvolucaoSalarial>(@"
        SELECT
            ano as Ano,
            mes as Mes,
            printf('%02d/%d', mes, ano) as Periodo,
            salario_base as SalarioBase,
            valor_liquido as ValorLiquido,
            total_vencimentos as TotalVencimentos,
            total_descontos as TotalDescontos
        FROM holerites
        WHERE user_id = @UserId AND tipo = 'MENSAL'
        ORDER BY ano, mes", new { UserId = userId });
    return Results.Ok(evolucao);
}).RequireAuthorization().WithTags("Evolução");

app.MapGet("/api/evolucao/liquido", async (HttpContext context, SqliteConnection db) =>
{
    var userId = GetUserId(context);
    if (userId == null) return Results.Unauthorized();

    await db.OpenAsync();
    var evolucao = await db.QueryAsync(@"
        SELECT
            ano,
            mes,
            printf('%02d/%d', mes, ano) as periodo,
            tipo,
            valor_liquido
        FROM holerites
        WHERE user_id = @UserId
        ORDER BY ano, mes, tipo", new { UserId = userId });
    return Results.Ok(evolucao);
}).RequireAuthorization().WithTags("Evolução");

app.MapGet("/api/estatisticas", async (HttpContext context, SqliteConnection db) =>
{
    var userId = GetUserId(context);
    if (userId == null) return Results.Unauthorized();

    await db.OpenAsync();

    var anoAtual = await db.QueryFirstOrDefaultAsync<int?>(
        "SELECT MAX(ano) FROM holerites WHERE user_id = @UserId", new { UserId = userId });

    if (anoAtual == null)
    {
        return Results.Ok(new Estatisticas());
    }

    var salarioAtual = await db.QueryFirstOrDefaultAsync<decimal?>(@"
        SELECT salario_base FROM holerites
        WHERE user_id = @UserId AND tipo = 'MENSAL'
        ORDER BY ano DESC, mes DESC LIMIT 1", new { UserId = userId }) ?? 0;

    var totalAnoAtual = await db.QueryFirstOrDefaultAsync<decimal?>(@"
        SELECT SUM(valor_liquido) FROM holerites
        WHERE user_id = @UserId AND ano = @anoAtual", new { UserId = userId, anoAtual }) ?? 0;

    var totalImpostos = await db.QueryFirstOrDefaultAsync<decimal?>(@"
        SELECT SUM(r.desconto) FROM rubricas r
        JOIN holerites h ON r.holerite_id = h.id
        WHERE h.user_id = @UserId AND h.ano = @anoAtual AND r.codigo IN ('998', '999')",
        new { UserId = userId, anoAtual }) ?? 0;

    var fgtsAcumulado = await db.QueryFirstOrDefaultAsync<decimal?>(
        "SELECT SUM(fgts_mes) FROM holerites WHERE user_id = @UserId",
        new { UserId = userId }) ?? 0;

    var mediaMensal = await db.QueryFirstOrDefaultAsync<decimal?>(@"
        SELECT AVG(valor_liquido) FROM holerites
        WHERE user_id = @UserId AND ano = @anoAtual AND tipo = 'MENSAL'",
        new { UserId = userId, anoAtual }) ?? 0;

    var primeiroSalario = await db.QueryFirstOrDefaultAsync<decimal?>(@"
        SELECT salario_base FROM holerites
        WHERE user_id = @UserId AND tipo = 'MENSAL'
        ORDER BY ano, mes LIMIT 1", new { UserId = userId }) ?? 0;

    var variacao = primeiroSalario > 0
        ? ((salarioAtual - primeiroSalario) / primeiroSalario) * 100
        : 0;

    var totalHolerites = await db.QueryFirstOrDefaultAsync<int>(
        "SELECT COUNT(*) FROM holerites WHERE user_id = @UserId", new { UserId = userId });

    return Results.Ok(new Estatisticas
    {
        SalarioAtual = salarioAtual,
        TotalRecebidoAnoAtual = totalAnoAtual,
        TotalImpostosAnoAtual = totalImpostos,
        FgtsAcumulado = fgtsAcumulado,
        MediaMensalAnoAtual = mediaMensal,
        VariacaoSalarial = variacao,
        TotalHolerites = totalHolerites
    });
}).RequireAuthorization().WithTags("Estatísticas");

app.MapGet("/api/beneficios", async (HttpContext context, SqliteConnection db) =>
{
    var userId = GetUserId(context);
    if (userId == null) return Results.Unauthorized();

    await db.OpenAsync();
    var beneficios = await db.QueryAsync(@"
        SELECT
            h.ano,
            r.descricao,
            SUM(r.vencimento) as total
        FROM rubricas r
        JOIN holerites h ON r.holerite_id = h.id
        WHERE h.user_id = @UserId
          AND r.vencimento > 0
          AND r.descricao NOT IN ('DIAS NORMAIS')
          AND r.descricao NOT LIKE '%13%'
        GROUP BY h.ano, r.descricao
        ORDER BY h.ano, total DESC", new { UserId = userId });
    return Results.Ok(beneficios);
}).RequireAuthorization().WithTags("Benefícios");

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" })).WithTags("Health");

app.Run();

// Resolve um caminho relativo a partir do diretório do binário (não do CWD)
static string ResolvePath(string path)
{
    if (Path.IsPathRooted(path)) return path;
    return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, path));
}
