#!/usr/bin/env python3
"""Verifica carga do serviço de Consolidado contra o ambiente em execução.

Nao roda em `dotnet test` e nao bloqueia build - e um instrumento de verificacao operacional
manual contra o ambiente rodando de verdade, nao um teste de regressao.

Dispara consultas a taxa constante e mede latencia e taxa de erro, contra as metas de p95 ate
100ms e taxa de erro ate 5%, confirmando que a carga de referencia de 50 req/s fica dentro dessas
metas. Ao mesmo tempo, simula uma carga de fundo de escrita no Lancamentos (10 req/s, 5
clientes simultaneos) nos mesmos dias consultados pelos 50 req/s - um consolidado parado, sem
lancamentos concorrentes chegando, mede um cenario que nao acontece em producao.

A credencial e obtida do emissor real (`POST /connect/token`, client_credentials) com o par
client_id/client_secret informado - o mesmo par da semente de `docker-compose.yml`. Uma unica
credencial e obtida e reaproveitada durante a corrida inteira, evitando que a emissao repetida de
credenciais interfira na medicao de carga.

Sem k6 e sem container adicional, e sem PyJWT: urllib, base64 e ThreadPoolExecutor da biblioteca
padrao bastam.

Uso:
    python3 scripts/carga_consolidado.py <client-id> <client-secret> --rps 50 --duracao 30
"""

import argparse
import base64
import json
import threading
import time
import urllib.error
import urllib.parse
import urllib.request
from concurrent.futures import ThreadPoolExecutor
from datetime import date, timedelta

URL_IDENTIDADE_PADRAO = "http://localhost:8082"
URL_LANCAMENTOS_PADRAO = "http://localhost:8080"
URL_CONSOLIDADO_PADRAO = "http://localhost:8081"

META_P95_MS = 100.0
META_TAXA_DE_ERRO = 0.05

JANELA_DE_DIAS_CONSULTADOS = 7
RPS_LANCAMENTOS_DE_FUNDO = 10
CLIENTES_LANCAMENTOS_DE_FUNDO = 5


def obter_token(base_url_identidade: str, client_id: str, client_secret: str) -> str:
    credenciais = base64.b64encode(f"{client_id}:{client_secret}".encode("utf-8")).decode("ascii")
    corpo = urllib.parse.urlencode({"grant_type": "client_credentials"}).encode("utf-8")

    requisicao = urllib.request.Request(
        f"{base_url_identidade}/connect/token",
        data=corpo,
        method="POST",
        headers={
            "Authorization": f"Basic {credenciais}",
            "Content-Type": "application/x-www-form-urlencoded",
        },
    )
    with urllib.request.urlopen(requisicao, timeout=10) as resposta:
        return json.loads(resposta.read())["access_token"]


def consultar_consolidado(base_url: str, token: str, data: str) -> tuple[int, float]:
    requisicao = urllib.request.Request(
        f"{base_url}/consolidado/{data}",
        headers={"Authorization": f"Bearer {token}"},
    )

    inicio = time.perf_counter()
    try:
        with urllib.request.urlopen(requisicao, timeout=10) as resposta:
            resposta.read()
            status = resposta.status
    except urllib.error.HTTPError as erro:
        status = erro.code
    except OSError:
        status = 0
    duracao_ms = (time.perf_counter() - inicio) * 1000

    return status, duracao_ms


def percentil(latencias_ordenadas: list[float], p: float) -> float:
    if not latencias_ordenadas:
        return 0.0
    indice = min(len(latencias_ordenadas) - 1, int(len(latencias_ordenadas) * p))
    return latencias_ordenadas[indice]


def dias_consultados(quantidade: int) -> list[str]:
    hoje = date.today()
    return [(hoje - timedelta(days=deslocamento)).isoformat() for deslocamento in range(quantidade)]


def registrar_lancamento_carga(base_url_lancamentos: str, token: str, dia: str, indice: int) -> tuple[int, float]:
    corpo = json.dumps(
        {
            "tipo": "credito" if indice % 2 == 0 else "debito",
            "valor": round(1.0 + (indice % 500) / 100, 2),
            "competencia": dia,
            "descricao": "carga de fundo",
        }
    ).encode("utf-8")

    requisicao = urllib.request.Request(
        f"{base_url_lancamentos}/lancamentos",
        data=corpo,
        method="POST",
        headers={
            "Authorization": f"Bearer {token}",
            "Idempotency-Key": f"carga-{time.time_ns()}-{indice}",
            "Content-Type": "application/json",
        },
    )

    inicio = time.perf_counter()
    try:
        with urllib.request.urlopen(requisicao, timeout=10) as resposta:
            resposta.read()
            status = resposta.status
    except urllib.error.HTTPError as erro:
        status = erro.code
    except OSError:
        status = 0
    duracao_ms = (time.perf_counter() - inicio) * 1000

    return status, duracao_ms


def executar_carga_lancamentos_de_fundo(
    token: str, base_url_lancamentos: str, duracao: int, dias: list[str], resultados: list[tuple[int, float]]
) -> None:
    total = RPS_LANCAMENTOS_DE_FUNDO * duracao
    intervalo = 1.0 / RPS_LANCAMENTOS_DE_FUNDO

    with ThreadPoolExecutor(max_workers=CLIENTES_LANCAMENTOS_DE_FUNDO) as executor:
        futuros = []
        inicio_geral = time.perf_counter()
        for indice in range(total):
            atraso = (inicio_geral + indice * intervalo) - time.perf_counter()
            if atraso > 0:
                time.sleep(atraso)
            dia = dias[indice % len(dias)]
            futuros.append(executor.submit(registrar_lancamento_carga, base_url_lancamentos, token, dia, indice))

        resultados.extend(futuro.result() for futuro in futuros)


def executar_carga(token: str, base_url_consolidado: str, base_url_lancamentos: str, rps: int, duracao: int) -> None:
    dias = dias_consultados(JANELA_DE_DIAS_CONSULTADOS)

    resultados_lancamentos: list[tuple[int, float]] = []
    thread_lancamentos = threading.Thread(
        target=executar_carga_lancamentos_de_fundo,
        args=(token, base_url_lancamentos, duracao, dias, resultados_lancamentos),
    )
    thread_lancamentos.start()

    total = rps * duracao
    intervalo = 1.0 / rps

    with ThreadPoolExecutor(max_workers=max(rps, 10)) as executor:
        futuros = []
        inicio_geral = time.perf_counter()
        for indice in range(total):
            atraso = (inicio_geral + indice * intervalo) - time.perf_counter()
            if atraso > 0:
                time.sleep(atraso)
            dia = dias[indice % len(dias)]
            futuros.append(executor.submit(consultar_consolidado, base_url_consolidado, token, dia))

        resultados = [futuro.result() for futuro in futuros]
    duracao_total = time.perf_counter() - inicio_geral

    thread_lancamentos.join()

    latencias_ordenadas = sorted(duracao_ms for _, duracao_ms in resultados)
    erros = sum(1 for status, _ in resultados if status < 200 or status >= 300)
    taxa_de_erro = erros / len(resultados)
    p95 = percentil(latencias_ordenadas, 0.95)

    print(f"consolidado — {rps} req/s em {len(dias)} dia(s), com {RPS_LANCAMENTOS_DE_FUNDO} req/s de "
          f"lançamentos de fundo ({CLIENTES_LANCAMENTOS_DE_FUNDO} clientes) nesses dias:")
    print(f"total de consultas ao consolidado: {len(resultados)}")
    print(f"erros: {erros} ({100 * taxa_de_erro:.2f}%)")
    print(f"p50: {percentil(latencias_ordenadas, 0.50):.1f}ms")
    print(f"p95: {p95:.1f}ms")
    print(f"p99: {percentil(latencias_ordenadas, 0.99):.1f}ms")
    print(f"req/s efetivo: {len(resultados) / duracao_total:.1f}")

    if taxa_de_erro > META_TAXA_DE_ERRO:
        print(f"AVISO: taxa de erro acima de {100 * META_TAXA_DE_ERRO:.0f}%")
    if p95 > META_P95_MS:
        print(f"AVISO: p95 acima de {META_P95_MS:.0f}ms")

    erros_lancamentos = sum(1 for status, _ in resultados_lancamentos if status < 200 or status >= 300)
    print()
    print("lançamentos de fundo (escrita concorrente simulada durante a carga):")
    print(f"total de lançamentos enviados: {len(resultados_lancamentos)}")
    print(f"erros: {erros_lancamentos} ({100 * erros_lancamentos / len(resultados_lancamentos):.2f}%)")


def main() -> None:
    parser = argparse.ArgumentParser(description="Verifica carga do serviço de Consolidado.")
    parser.add_argument("client_id")
    parser.add_argument("client_secret")
    parser.add_argument("--rps", type=int, default=50)
    parser.add_argument("--duracao", type=int, default=30)
    parser.add_argument("--url-identidade", default=URL_IDENTIDADE_PADRAO)
    parser.add_argument("--url-lancamentos", default=URL_LANCAMENTOS_PADRAO)
    parser.add_argument("--url-consolidado", default=URL_CONSOLIDADO_PADRAO)
    args = parser.parse_args()

    token = obter_token(args.url_identidade, args.client_id, args.client_secret)

    executar_carga(token, args.url_consolidado, args.url_lancamentos, args.rps, args.duracao)


if __name__ == "__main__":
    main()
