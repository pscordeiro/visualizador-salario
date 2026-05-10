namespace Api.Models;

public class ArquivoPdf
{
    public string Id { get; set; } = "";
    public string UserId { get; set; } = "";
    public string NomeOriginal { get; set; } = "";
    public string NomeStorage { get; set; } = "";
    public long TamanhoBytes { get; set; }
    public string Status { get; set; } = "pendente";
    public string? ErroMensagem { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
}

public class ArquivoPdfDto
{
    public string Id { get; set; } = "";
    public string NomeOriginal { get; set; } = "";
    public long TamanhoBytes { get; set; }
    public string Status { get; set; } = "";
    public string? ErroMensagem { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public int HoleritesCount { get; set; }
    public int? Ano { get; set; }
    public int? Mes { get; set; }
    public string? Tipo { get; set; }
    public string? Referencia { get; set; }
}
