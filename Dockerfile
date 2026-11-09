# Imagem da API PagUai (monólito). Build em duas etapas: SDK para compilar, runtime para executar.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json PagUai.slnx ./
COPY src/PagUai.Api/PagUai.Api.csproj src/PagUai.Api/
RUN dotnet restore src/PagUai.Api/PagUai.Api.csproj
COPY src/ src/
RUN dotnet publish src/PagUai.Api/PagUai.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "PagUai.Api.dll"]
