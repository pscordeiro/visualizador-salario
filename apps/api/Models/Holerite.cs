namespace Api.Models;

public class Holerite
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public string? ArquivoId { get; set; }
    public int Ano { get; set; }
    public int Mes { get; set; }
    public string Tipo { get; set; } = "";
    public string Empresa { get; set; } = "";
    public string Cnpj { get; set; } = "";
    public string Cargo { get; set; } = "";
    public decimal SalarioBase { get; set; }
    public decimal TotalVencimentos { get; set; }
    public decimal TotalDescontos { get; set; }
    public decimal ValorLiquido { get; set; }
    public decimal BaseInss { get; set; }
    public decimal BaseFgts { get; set; }
    public decimal FgtsMes { get; set; }
    public decimal BaseIrrf { get; set; }
    public decimal FaixaIrrf { get; set; }
    public string ArquivoOrigem { get; set; } = "";
}

public class Rubrica
{
    public int Id { get; set; }
    public int HoleriteId { get; set; }
    public string Codigo { get; set; } = "";
    public string Descricao { get; set; } = "";
    public string Referencia { get; set; } = "";
    public decimal Vencimento { get; set; }
    public decimal Desconto { get; set; }
}

public class ResumoAnual
{
    public int Ano { get; set; }
    public decimal TotalLiquidoMensal { get; set; }
    public decimal Total13 { get; set; }
    public decimal TotalFerias { get; set; }
    public decimal Total14 { get; set; }
    public decimal TotalGeral { get; set; }
    public decimal TotalDescontos { get; set; }
    public decimal TotalFgts { get; set; }
}

public class EvolucaoSalarial
{
    public int Ano { get; set; }
    public int Mes { get; set; }
    public string Periodo { get; set; } = "";
    public decimal SalarioBase { get; set; }
    public decimal ValorLiquido { get; set; }
    public decimal TotalVencimentos { get; set; }
    public decimal TotalDescontos { get; set; }
}

public class Estatisticas
{
    public decimal SalarioAtual { get; set; }
    public decimal TotalRecebidoAnoAtual { get; set; }
    public decimal TotalImpostosAnoAtual { get; set; }
    public decimal FgtsAcumulado { get; set; }
    public decimal MediaMensalAnoAtual { get; set; }
    public decimal VariacaoSalarial { get; set; }
    public int TotalHolerites { get; set; }
}

public class ResumoImpostos
{
    public int Ano { get; set; }
    public decimal TotalInss { get; set; }
    public decimal TotalIrrf { get; set; }
    public decimal TotalImpostos { get; set; }
}

public class ImpostoMensal
{
    public int Ano { get; set; }
    public int Mes { get; set; }
    public string Tipo { get; set; } = "";
    public string Periodo { get; set; } = "";
    public decimal Inss { get; set; }
    public decimal Irrf { get; set; }
    public decimal Total { get; set; }
    public decimal Bruto { get; set; }
    public decimal Liquido { get; set; }
}
