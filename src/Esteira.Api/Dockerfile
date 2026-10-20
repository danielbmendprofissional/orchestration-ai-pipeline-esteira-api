FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY Esteira.slnx global.json ./
COPY src/ src/
COPY scripts/ scripts/
RUN dotnet restore "src/Esteira.Api/Esteira.Api.csproj"
RUN dotnet build "src/Esteira.Api/Esteira.Api.csproj" -c $BUILD_CONFIGURATION --no-restore -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "src/Esteira.Api/Esteira.Api.csproj" -c $BUILD_CONFIGURATION --no-restore -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Esteira.Api.dll"]
