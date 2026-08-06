# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY FluxoCaixa.Contratos/FluxoCaixa.Contratos.csproj FluxoCaixa.Contratos/
COPY FluxoCaixa.Plataforma/FluxoCaixa.Plataforma.csproj FluxoCaixa.Plataforma/
COPY FluxoCaixa.Consolidado.Api/FluxoCaixa.Consolidado.Api.csproj FluxoCaixa.Consolidado.Api/
RUN dotnet restore FluxoCaixa.Consolidado.Api/FluxoCaixa.Consolidado.Api.csproj

COPY FluxoCaixa.Contratos/ FluxoCaixa.Contratos/
COPY FluxoCaixa.Plataforma/ FluxoCaixa.Plataforma/
COPY FluxoCaixa.Consolidado.Api/ FluxoCaixa.Consolidado.Api/
RUN dotnet publish FluxoCaixa.Consolidado.Api/FluxoCaixa.Consolidado.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=build /app .

ENTRYPOINT ["dotnet", "FluxoCaixa.Consolidado.Api.dll"]
