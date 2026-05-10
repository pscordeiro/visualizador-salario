#!/usr/bin/env python3
"""CLI para extração de holerite em PDF e gravação no banco SQLite.

Uso:
    python main.py --file <pdf> --user-id <uuid> --arquivo-id <uuid> --db <path>

Códigos de saída:
    0 — sucesso
    1 — erro genérico (PDF inválido, falha de I/O, etc.)
    2 — duplicata: já existe holerite com mesma referência (ano/mês/tipo)
"""

import argparse
import sqlite3
import sys
from pathlib import Path

from models import Holerite
from parser import parse_pdf


TIPO_DESCRICAO = {
    "MENSAL": "Mensal",
    "FERIAS": "Férias",
    "13A": "13º Adiantamento",
    "13I": "13º Integral",
    "14S": "14º Salário",
}


def conectar(db_path: Path) -> sqlite3.Connection:
    """Abre conexão com o SQLite (pressupõe schema já criado pelo backend)."""
    db_path.parent.mkdir(parents=True, exist_ok=True)
    return sqlite3.connect(db_path)


def existe_duplicata(conn: sqlite3.Connection, user_id: str, ano: int, mes: int, tipo: str) -> bool:
    cursor = conn.cursor()
    cursor.execute(
        "SELECT 1 FROM holerites WHERE user_id = ? AND ano = ? AND mes = ? AND tipo = ? LIMIT 1",
        (user_id, ano, mes, tipo),
    )
    return cursor.fetchone() is not None


def inserir_holerite(
    conn: sqlite3.Connection,
    holerite: Holerite,
    user_id: str,
    arquivo_id: str,
) -> int | None:
    cursor = conn.cursor()
    cursor.execute(
        """
        INSERT INTO holerites (
            user_id, arquivo_id, ano, mes, tipo, empresa, cnpj, cargo, salario_base,
            total_vencimentos, total_descontos, valor_liquido,
            base_inss, base_fgts, fgts_mes, base_irrf, faixa_irrf, arquivo_origem
        ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
        """,
        (
            user_id, arquivo_id, holerite.ano, holerite.mes, holerite.tipo,
            holerite.empresa, holerite.cnpj, holerite.cargo, holerite.salario_base,
            holerite.total_vencimentos, holerite.total_descontos, holerite.valor_liquido,
            holerite.base_inss, holerite.base_fgts, holerite.fgts_mes,
            holerite.base_irrf, holerite.faixa_irrf, holerite.arquivo_origem,
        ),
    )

    holerite_id = cursor.lastrowid
    if holerite_id is None:
        return None

    for r in holerite.rubricas:
        cursor.execute(
            """
            INSERT INTO rubricas (holerite_id, codigo, descricao, referencia, vencimento, desconto)
            VALUES (?, ?, ?, ?, ?, ?)
            """,
            (holerite_id, r.codigo, r.descricao, r.referencia, r.vencimento, r.desconto),
        )

    conn.commit()
    return holerite_id


def processar(file_path: Path, user_id: str, arquivo_id: str, db_path: Path) -> int:
    if not file_path.exists():
        print(f"ERRO: arquivo não encontrado: {file_path}", file=sys.stderr)
        return 1

    print(f"Processando: {file_path.name}")
    holerite = parse_pdf(file_path)
    if holerite is None:
        print("ERRO: falha ao parsear o PDF", file=sys.stderr)
        return 1

    with conectar(db_path) as conn:
        if existe_duplicata(conn, user_id, holerite.ano, holerite.mes, holerite.tipo):
            tipo_label = TIPO_DESCRICAO.get(holerite.tipo, holerite.tipo)
            print(
                f"ERRO_DUPLICATA: já existe um holerite de {tipo_label} para "
                f"{holerite.mes:02d}/{holerite.ano}",
                file=sys.stderr,
            )
            return 2

        try:
            inserir_holerite(conn, holerite, user_id, arquivo_id)
        except sqlite3.Error as e:
            print(f"ERRO: falha ao inserir no banco: {e}", file=sys.stderr)
            conn.rollback()
            return 1

    print(
        f"OK ({holerite.tipo} {holerite.mes:02d}/{holerite.ano} - "
        f"R$ {holerite.valor_liquido:,.2f})"
    )
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description="Extrator de holerites em PDF")
    parser.add_argument("--file", required=True, help="Caminho do PDF a processar")
    parser.add_argument("--user-id", required=True, help="ID do usuário dono do arquivo")
    parser.add_argument("--arquivo-id", required=True, help="ID do registro de arquivo no banco")
    parser.add_argument("--db", required=True, help="Caminho do arquivo SQLite")

    args = parser.parse_args()
    return processar(
        file_path=Path(args.file),
        user_id=args.user_id,
        arquivo_id=args.arquivo_id,
        db_path=Path(args.db),
    )


if __name__ == "__main__":
    sys.exit(main())
