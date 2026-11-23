# Imagem de um serviço da PagUai. O mesmo Dockerfile serve para todos: o argumento PROJETO
# escolhe qual (PagUai.Api, PagUai.Extrato, PagUai.Notificacoes ou PagUai.Gateway). Veja o docker-compose.yml.
# Build em duas etapas: SDK para compilar, runtime para executar.
ARG PROJETO=PagUai.Api

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG PROJETO
WORKDIR /src
COPY global.json ./
# Sprint 3: a biblioteca de mensageria é usada pelo monólito, pelo Extrato e por Notificações.
COPY src/PagUai.Mensageria/PagUai.Mensageria.csproj src/PagUai.Mensageria/
COPY src/${PROJETO}/${PROJETO}.csproj src/${PROJETO}/
RUN dotnet restore src/${PROJETO}/${PROJETO}.csproj
COPY contratos/ contratos/
COPY src/PagUai.Mensageria/ src/PagUai.Mensageria/
COPY src/${PROJETO}/ src/${PROJETO}/
RUN dotnet publish src/${PROJETO}/${PROJETO}.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
ARG PROJETO
ENV PAGUAI_DLL=${PROJETO}.dll
WORKDIR /app
COPY --from=build /app .
# A imagem do .NET 10 já escuta na porta 8080 (variável HTTP_PORTS). O Worker de Notificações não usa porta.
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["sh", "-c", "exec dotnet \"$PAGUAI_DLL\""]
