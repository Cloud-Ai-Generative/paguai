# Imagem de um serviço da PagUai. O mesmo Dockerfile serve para todos: o argumento PROJETO
# escolhe qual (PagUai.Api, PagUai.Extrato ou PagUai.Gateway). Veja o docker-compose.yml.
# Build em duas etapas: SDK para compilar, runtime para executar.
ARG PROJETO=PagUai.Api

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG PROJETO
WORKDIR /src
COPY global.json ./
COPY src/${PROJETO}/${PROJETO}.csproj src/${PROJETO}/
RUN dotnet restore src/${PROJETO}/${PROJETO}.csproj
COPY contratos/ contratos/
COPY src/${PROJETO}/ src/${PROJETO}/
RUN dotnet publish src/${PROJETO}/${PROJETO}.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
ARG PROJETO
ENV PAGUAI_DLL=${PROJETO}.dll
WORKDIR /app
COPY --from=build /app .
# A imagem do .NET 10 já escuta na porta 8080 (variável HTTP_PORTS).
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["sh", "-c", "exec dotnet \"$PAGUAI_DLL\""]
