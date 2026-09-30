# Imagem do Server (API + painel) para deploy em Railway, Render, Fly.io ou VPS com Docker.
#   docker build -t gestao-sorveteria .
#   docker run -p 8080:8080 -e ConnectionStrings__Default=... -e Jwt__Key=... -e Seed__AdminSenha=... gestao-sorveteria

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restaura primeiro só com os .csproj (cache de camadas do Docker)
COPY Directory.Build.props global.json ./
COPY src/GestaoSorveteria.Domain/GestaoSorveteria.Domain.csproj src/GestaoSorveteria.Domain/
COPY src/GestaoSorveteria.Contracts/GestaoSorveteria.Contracts.csproj src/GestaoSorveteria.Contracts/
COPY src/GestaoSorveteria.Application/GestaoSorveteria.Application.csproj src/GestaoSorveteria.Application/
COPY src/GestaoSorveteria.Infrastructure/GestaoSorveteria.Infrastructure.csproj src/GestaoSorveteria.Infrastructure/
COPY src/GestaoSorveteria.Server/GestaoSorveteria.Server.csproj src/GestaoSorveteria.Server/
RUN dotnet restore src/GestaoSorveteria.Server/GestaoSorveteria.Server.csproj

COPY src/ src/
RUN dotnet publish src/GestaoSorveteria.Server/GestaoSorveteria.Server.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_HTTP_PORTS=8080
ENV TZ=America/Sao_Paulo
EXPOSE 8080

ENTRYPOINT ["dotnet", "GestaoSorveteria.Server.dll"]
