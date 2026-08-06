# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY FluxoCaixa.Contratos/FluxoCaixa.Contratos.csproj FluxoCaixa.Contratos/
COPY FluxoCaixa.Plataforma/FluxoCaixa.Plataforma.csproj FluxoCaixa.Plataforma/
COPY FluxoCaixa.Lancamentos.Dominio/FluxoCaixa.Lancamentos.Dominio.csproj FluxoCaixa.Lancamentos.Dominio/
COPY FluxoCaixa.Lancamentos.Aplicacao/FluxoCaixa.Lancamentos.Aplicacao.csproj FluxoCaixa.Lancamentos.Aplicacao/
COPY FluxoCaixa.Lancamentos.Infraestrutura/FluxoCaixa.Lancamentos.Infraestrutura.csproj FluxoCaixa.Lancamentos.Infraestrutura/
COPY FluxoCaixa.Lancamentos.Api/FluxoCaixa.Lancamentos.Api.csproj FluxoCaixa.Lancamentos.Api/
RUN dotnet restore FluxoCaixa.Lancamentos.Api/FluxoCaixa.Lancamentos.Api.csproj

COPY FluxoCaixa.Contratos/ FluxoCaixa.Contratos/
COPY FluxoCaixa.Plataforma/ FluxoCaixa.Plataforma/
COPY FluxoCaixa.Lancamentos.Dominio/ FluxoCaixa.Lancamentos.Dominio/
COPY FluxoCaixa.Lancamentos.Aplicacao/ FluxoCaixa.Lancamentos.Aplicacao/
COPY FluxoCaixa.Lancamentos.Infraestrutura/ FluxoCaixa.Lancamentos.Infraestrutura/
COPY FluxoCaixa.Lancamentos.Api/ FluxoCaixa.Lancamentos.Api/
RUN dotnet publish FluxoCaixa.Lancamentos.Api/FluxoCaixa.Lancamentos.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# A janela de competência depende da conversão para America/Sao_Paulo; garante a base de fusos
# horários mesmo que a imagem base venha sem ela.
RUN apt-get update \
    && apt-get install -y --no-install-recommends tzdata \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app .

ENTRYPOINT ["dotnet", "FluxoCaixa.Lancamentos.Api.dll"]
