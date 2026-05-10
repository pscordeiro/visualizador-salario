"""Parser genérico de holerites em PDF (modelo brasileiro)."""

import re
from pathlib import Path

import pdfplumber

from models import Holerite, Rubrica


MESES_PT = {
    "janeiro": 1, "fevereiro": 2, "março": 3, "marco": 3, "abril": 4,
    "maio": 5, "junho": 6, "julho": 7, "agosto": 8, "setembro": 9,
    "outubro": 10, "novembro": 11, "dezembro": 12,
}


def parse_valor(texto: str) -> float:
    """Converte valor em formato brasileiro (1.234,56) para float."""
    if not texto or not texto.strip():
        return 0.0
    limpo = texto.strip().replace(".", "").replace(",", ".")
    try:
        return float(limpo)
    except ValueError:
        return 0.0


def determinar_tipo_folha(nome_arquivo: str, texto: str) -> str:
    """Detecta o tipo de folha a partir do conteúdo (com fallback ao nome)."""
    nome = nome_arquivo.lower()
    txt = texto.lower()

    if "recibo de férias" in txt or "recibo de ferias" in txt or "férias" in nome or "ferias" in nome:
        return "FERIAS"
    if "13o. adiantamento" in txt or "13º adiantamento" in txt or "adiantamento de 13" in txt or "-13a-" in nome:
        return "13A"
    if "13o. integral" in txt or "13º integral" in txt or "13o salario" in txt or "13º salário" in txt or "-13i-" in nome:
        return "13I"
    if "complementar" in txt or "14º" in txt or "plr" in txt or "-c-" in nome or "-14-" in nome:
        return "14S"
    return "MENSAL"


def extrair_mes_ano(nome_arquivo: str, texto: str) -> tuple[int, int]:
    """Extrai mês e ano a partir do conteúdo (preferencial) e do nome do arquivo."""
    # 1) "Competência: MM/YYYY"
    m = re.search(r"compet[êe]ncia[:\s]+(\d{1,2})\s*[/-]\s*(\d{4})", texto, re.IGNORECASE)
    if m:
        return int(m.group(1)), int(m.group(2))

    # 2) Mês por extenso + ano
    for nome_mes, num_mes in MESES_PT.items():
        m = re.search(rf"{nome_mes}[\s/]+(?:de\s+)?(\d{{4}})", texto, re.IGNORECASE)
        if m:
            return num_mes, int(m.group(1))

    # 3) Padrão genérico no nome: ...MM-YYYY... / MM_YYYY / MM/YYYY
    m = re.search(r"(?<!\d)(\d{1,2})[-_/](\d{4})(?!\d)", nome_arquivo)
    if m:
        mes = int(m.group(1))
        ano = int(m.group(2))
        if 1 <= mes <= 12 and 1990 <= ano <= 2100:
            return mes, ano

    # 4) Padrão YYYY-MM no nome
    m = re.search(r"(\d{4})[-_/](\d{1,2})(?!\d)", nome_arquivo)
    if m:
        ano = int(m.group(1))
        mes = int(m.group(2))
        if 1 <= mes <= 12 and 1990 <= ano <= 2100:
            return mes, ano

    return 0, 0


def extrair_empresa_cnpj(texto: str) -> tuple[str, str]:
    """Extrai nome da empresa e CNPJ de forma genérica."""
    cnpj = ""
    empresa = ""

    m = re.search(r"CNPJ[:\s]*([\d./-]{14,20})", texto)
    if m:
        cnpj = m.group(1).strip()

    # Empresa: linha em CAIXA ALTA imediatamente acima do CNPJ
    if cnpj:
        idx = texto.find(cnpj)
        antes = texto[:idx]
        for linha in reversed([l.strip() for l in antes.splitlines() if l.strip()]):
            if (linha.isupper() and len(linha) >= 4 and
                    not any(c.isdigit() for c in linha) and
                    not linha.startswith(("RUA", "AV", "AVENIDA", "ENDEREÇO", "ENDERECO"))):
                empresa = linha
                break

    if not empresa:
        m = re.search(r"(?:Empresa|Raz[ãa]o\s+Social)[:\s]+([^\n]{4,80})", texto, re.IGNORECASE)
        if m:
            empresa = m.group(1).strip()

    return empresa, cnpj


def extrair_cargo(texto: str) -> str:
    """Extrai o cargo via labels comuns (Cargo, Função, CBO)."""
    patterns = [
        r"(?:Cargo|Fun[çc][ãa]o|Ocupa[çc][ãa]o)[:\s]+([^\n]{3,60})",
        r"CBO[:\s]+\d+[\s/-]+([^\n]{3,60})",
    ]
    for pat in patterns:
        m = re.search(pat, texto, re.IGNORECASE)
        if m:
            cargo = m.group(1).strip()
            cargo = re.split(
                r"\s+(?:Admiss[ãa]o|CBO|C[óo]digo|Departamento|Setor)\b",
                cargo, maxsplit=1, flags=re.IGNORECASE,
            )[0]
            return cargo.strip(" -:|")
    return ""


def _zero_valores() -> dict:
    return {
        "salario_base": 0.0, "total_vencimentos": 0.0, "total_descontos": 0.0,
        "valor_liquido": 0.0, "base_inss": 0.0, "base_fgts": 0.0,
        "fgts_mes": 0.0, "base_irrf": 0.0, "faixa_irrf": 0.0,
    }


def extrair_valores_ferias(texto: str) -> dict:
    """Recibos de férias têm estrutura diferente do holerite mensal."""
    valores = _zero_valores()

    m = re.search(r"Sal[áa]rio\s+Base[:\s]+([\d.,]+)", texto)
    if m:
        valores["salario_base"] = parse_valor(m.group(1))

    m = re.search(r"TOTAL\s+DOS\s+PROVENTOS[:\s]+([\d.,]+)", texto, re.IGNORECASE)
    if m:
        valores["total_vencimentos"] = parse_valor(m.group(1))

    m = re.search(r"TOTAL\s+DOS\s+DESCONTOS[:\s]+([\d.,]+)", texto, re.IGNORECASE)
    if m:
        valores["total_descontos"] = parse_valor(m.group(1))

    m = re.search(r"TOTAL\s+L[ÍI]QUIDO[:\s]+([\d.,]+)", texto, re.IGNORECASE)
    if m:
        valores["valor_liquido"] = parse_valor(m.group(1))

    valores["base_inss"] = valores["salario_base"]
    return valores


def extrair_rubricas_ferias(texto: str) -> list[Rubrica]:
    """Rubricas de recibo de férias."""
    rubricas: list[Rubrica] = []

    def add(codigo: str, descricao: str, vencimento: float = 0.0, desconto: float = 0.0):
        if vencimento > 0 or desconto > 0:
            rubricas.append(Rubrica(
                codigo=codigo, descricao=descricao,
                referencia="", vencimento=vencimento, desconto=desconto,
            ))

    m = re.search(r"F[ée]rias[:\s]+([\d.,]+)\s*P", texto)
    if m:
        add("FERIAS", "FÉRIAS", vencimento=parse_valor(m.group(1)))

    m = re.search(r"1/3\s+das\s+F[ée]rias[:\s]+([\d.,]+)", texto)
    if m:
        add("FERIAS13", "1/3 FÉRIAS", vencimento=parse_valor(m.group(1)))

    m = re.search(r"Abono\s+de\s+F[ée]rias[:\s]+([\d.,]+)", texto)
    if m:
        add("ABONO", "ABONO DE FÉRIAS", vencimento=parse_valor(m.group(1)))

    m = re.search(r"Desconto\s+da\s+Previd[êe]ncia[:\s]+([\d.,]+)", texto)
    if m:
        add("998", "INSS", desconto=parse_valor(m.group(1)))

    m = re.search(r"Desconto\s+do\s+(?:imposto\s+de\s+Renda|IRRF)[:\s]+([\d.,]+)", texto, re.IGNORECASE)
    if m:
        add("999", "IRRF", desconto=parse_valor(m.group(1)))

    return rubricas


def extrair_valores_totais_tabela(pdf) -> dict:
    """Extrai totais varrendo as tabelas (mais preciso que regex no texto)."""
    valores = _zero_valores()

    for page in pdf.pages:
        for table in page.extract_tables() or []:
            for row in table:
                if not row:
                    continue
                for cell in row:
                    if not cell:
                        continue
                    s = str(cell)

                    if "Total de Vencimentos" in s and valores["total_vencimentos"] == 0:
                        m = re.search(r"Total de Vencimentos\s*\n?\s*([\d.,]+)", s)
                        if m:
                            valores["total_vencimentos"] = parse_valor(m.group(1))

                    if "Total de Descontos" in s and valores["total_descontos"] == 0:
                        m = re.search(r"Total de Descontos\s*\n?\s*([\d.,]+)", s)
                        if m:
                            valores["total_descontos"] = parse_valor(m.group(1))

                    if ("Valor Líquido" in s or "Valor Liquido" in s) and valores["valor_liquido"] == 0:
                        m = re.search(r"Valor\s+L[íi]quido\s*\n?\s*([\d.,]+)", s)
                        if m:
                            valores["valor_liquido"] = parse_valor(m.group(1))

        if valores["valor_liquido"] == 0:
            for table in page.extract_tables() or []:
                for row in table:
                    if not row:
                        continue
                    for i, cell in enumerate(row):
                        if cell and "Valor Líquido" in str(cell):
                            for offset in range(1, 4):
                                if i + offset < len(row) and row[i + offset]:
                                    val = str(row[i + offset]).strip()
                                    if re.fullmatch(r"[\d.,]+", val):
                                        valores["valor_liquido"] = parse_valor(val)
                                        break
                            if valores["valor_liquido"] > 0:
                                break
                    if valores["valor_liquido"] > 0:
                        break

    return valores


def _extrair_bases_texto(texto: str, valores: dict) -> None:
    """Extrai salário base e bases (INSS/FGTS/IRRF) das linhas-resumo do holerite."""
    linhas = texto.split("\n")
    for i, linha in enumerate(linhas):
        if "Salário Base" in linha and "INSS" in linha and "FGTS" in linha:
            for j in range(i + 1, min(i + 5, len(linhas))):
                proxima = linhas[j].strip()
                nums = re.findall(r"[\d.,]+", proxima)
                if len(nums) >= 5:
                    valores["salario_base"] = parse_valor(nums[0])
                    valores["base_inss"] = parse_valor(nums[1])
                    valores["base_fgts"] = parse_valor(nums[2])
                    valores["fgts_mes"] = parse_valor(nums[3])
                    valores["base_irrf"] = parse_valor(nums[4])
                    if len(nums) >= 6:
                        valores["faixa_irrf"] = parse_valor(nums[5])
                    return


def extrair_valores_totais(texto: str, pdf=None) -> dict:
    """Extrai totais — tenta tabela primeiro, depois regex no texto."""
    if pdf:
        valores = extrair_valores_totais_tabela(pdf)
        if valores["total_vencimentos"] > 0:
            _extrair_bases_texto(texto, valores)
            return valores

    valores = _zero_valores()
    _extrair_bases_texto(texto, valores)

    if valores["total_vencimentos"] == 0:
        m = re.search(r"Total\s+de\s+Vencimentos\s*([\d.,]+)", texto)
        if m:
            valores["total_vencimentos"] = parse_valor(m.group(1))

    if valores["total_descontos"] == 0:
        m = re.search(r"Total\s+de\s+Descontos\s*([\d.,]+)", texto)
        if m:
            valores["total_descontos"] = parse_valor(m.group(1))

    if valores["valor_liquido"] == 0:
        m = re.search(r"Valor\s+L[íi]quido\s*([\d.,]+)", texto)
        if m:
            valores["valor_liquido"] = parse_valor(m.group(1))

    return valores


def extrair_rubricas(pdf) -> list[Rubrica]:
    """Extrai rubricas (códigos, descrições, vencimentos e descontos) das tabelas."""
    rubricas: list[Rubrica] = []

    for page in pdf.pages:
        for table in page.extract_tables() or []:
            for row in table:
                if not row or len(row) < 4:
                    continue

                codigos_cell = str(row[0]).strip() if row[0] else ""
                if not codigos_cell or codigos_cell in ("Código", "None"):
                    continue

                codigos = [c.strip() for c in codigos_cell.split("\n")]
                if not codigos[0].isdigit():
                    continue

                descricoes_cell = ""
                for cell in row[1:5]:
                    if cell and str(cell).strip() and not re.fullmatch(r"[\d.,\s\n]+", str(cell)):
                        descricoes_cell = str(cell).strip()
                        break

                descricoes = descricoes_cell.split("\n") if descricoes_cell else []

                referencias_list: list[str] = []
                vencimentos_list: list[str] = []
                descontos_list: list[str] = []

                for cell in row[2:]:
                    if not cell:
                        continue
                    s = str(cell).strip()
                    if re.fullmatch(r"[\d.,\n\s]+", s) and "," in s:
                        valores = [v.strip() for v in s.split("\n") if v.strip()]
                        if not referencias_list:
                            referencias_list = valores
                        elif not vencimentos_list:
                            vencimentos_list = valores
                        elif not descontos_list:
                            descontos_list = valores

                num_vencimentos = len(vencimentos_list)

                for i, codigo in enumerate(codigos):
                    if not codigo.isdigit():
                        continue
                    descricao = descricoes[i].strip() if i < len(descricoes) else ""
                    vencimento = 0.0
                    desconto = 0.0

                    if i < num_vencimentos:
                        vencimento = parse_valor(vencimentos_list[i])
                    else:
                        idx = i - num_vencimentos
                        if idx < len(descontos_list):
                            desconto = parse_valor(descontos_list[idx])

                    if descricao and (vencimento > 0 or desconto > 0):
                        rubricas.append(Rubrica(
                            codigo=codigo, descricao=descricao,
                            referencia="", vencimento=vencimento, desconto=desconto,
                        ))

    seen = set()
    unicos: list[Rubrica] = []
    for r in rubricas:
        key = (r.codigo, r.descricao, r.vencimento, r.desconto)
        if key not in seen:
            seen.add(key)
            unicos.append(r)

    return unicos


def parse_pdf(caminho: Path) -> Holerite | None:
    """Extrai um Holerite a partir de um PDF de contracheque."""
    try:
        with pdfplumber.open(caminho) as pdf:
            texto = "".join((page.extract_text() or "") for page in pdf.pages)

            nome = caminho.name
            tipo = determinar_tipo_folha(nome, texto)
            mes, ano = extrair_mes_ano(nome, texto)
            if mes == 0 or ano == 0:
                print(f"AVISO: não foi possível identificar mês/ano em {nome}")
                return None

            empresa, cnpj = extrair_empresa_cnpj(texto)
            cargo = extrair_cargo(texto)

            if tipo == "FERIAS":
                valores = extrair_valores_ferias(texto)
                rubricas = extrair_rubricas_ferias(texto)
            else:
                valores = extrair_valores_totais(texto, pdf)
                rubricas = extrair_rubricas(pdf)

            return Holerite(
                ano=ano, mes=mes, tipo=tipo,
                empresa=empresa, cnpj=cnpj, cargo=cargo,
                salario_base=valores["salario_base"],
                total_vencimentos=valores["total_vencimentos"],
                total_descontos=valores["total_descontos"],
                valor_liquido=valores["valor_liquido"],
                base_inss=valores["base_inss"],
                base_fgts=valores["base_fgts"],
                fgts_mes=valores["fgts_mes"],
                base_irrf=valores["base_irrf"],
                faixa_irrf=valores["faixa_irrf"],
                arquivo_origem=nome,
                rubricas=rubricas,
            )
    except Exception as e:
        print(f"Erro ao processar {caminho}: {e}")
        return None
