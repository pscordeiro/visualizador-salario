from dataclasses import dataclass, field
from typing import Optional


@dataclass
class Rubrica:
    codigo: str
    descricao: str
    referencia: str
    vencimento: float = 0.0
    desconto: float = 0.0


@dataclass
class Holerite:
    ano: int
    mes: int
    tipo: str  # 'MENSAL', '13A', '13I', 'FERIAS', 'COMPLEMENTAR'
    empresa: str
    cnpj: str
    cargo: str
    salario_base: float
    total_vencimentos: float
    total_descontos: float
    valor_liquido: float
    base_inss: float
    base_fgts: float
    fgts_mes: float
    base_irrf: float
    faixa_irrf: float
    arquivo_origem: str
    rubricas: list[Rubrica] = field(default_factory=list)
