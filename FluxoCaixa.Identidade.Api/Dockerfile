# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY FluxoCaixa.Plataforma/FluxoCaixa.Plataforma.csproj FluxoCaixa.Plataforma/
COPY FluxoCaixa.Identidade.Api/FluxoCaixa.Identidade.Api.csproj FluxoCaixa.Identidade.Api/
RUN dotnet restore FluxoCaixa.Identidade.Api/FluxoCaixa.Identidade.Api.csproj

COPY FluxoCaixa.Plataforma/ FluxoCaixa.Plataforma/
COPY FluxoCaixa.Identidade.Api/ FluxoCaixa.Identidade.Api/
RUN dotnet publish FluxoCaixa.Identidade.Api/FluxoCaixa.Identidade.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=build /app .

ENTRYPOINT ["dotnet", "FluxoCaixa.Identidade.Api.dll"]
