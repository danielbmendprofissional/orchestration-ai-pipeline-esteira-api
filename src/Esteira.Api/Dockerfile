# imagem sdk completa para o build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
# Build config fixo porque a ideia é um artefato único até chegar em produçõa
# ai precisa ja ser o release mesmo.
ARG BUILD_CONFIGURATION=Release
# aqui é tipo o CD para trabalhar dentro do src
WORKDIR /src
# aqui eu falei "joga esse solution e esse json AQUI, esse "./" é o "aqui"
# como eu entrei dentro de src do WORKDIR agora eu falei joga isso ai aqui
COPY Esteira.slnx global.json ./
# ai aqui eu falei: joga a pasta /src da minha maquin
# dentro de uma nova /src do workdir, isso também no script ficando assim:
#
# /src
# ├─ Esteira.slnx
# ├─ global.json
# ├─ src/       (todos os projetos)
# └─ scripts/
COPY src/ src/
COPY scripts/ scripts/

# Aqui ele baixa os pacotes e guarda no cache e /obj
RUN dotnet restore "src/Esteira.Api/Esteira.Api.csproj"

# Aqui ele faz o build e deixa em app/build/
RUN dotnet build "src/Esteira.Api/Esteira.Api.csproj" -c $BUILD_CONFIGURATION --no-restore -o /app/build


# Ai esse aqui está só setando o runtime, não executa, mas ai fala a imagem,
# fala o user que vai rodar o restante, fala onde a aplicação vai rodar (/app)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
#aqui ele fala as portas dessa aplicação
EXPOSE 8080
# vou deixar só uma mesmo não preciso de mais
# EXPOSE 8081

# ai aqui ele usa a imagem do build e publica
FROM build AS publish
# aqui ele repete porque na pratica o build gerou um obj/Release, mas
# poderia muito bem ter obj/debug, eu to informando que ele vai usar o release
ARG BUILD_CONFIGURATION=Release
# aqui eu deixo as dlls publicads e o que precisa pra rodar em app/publish
RUN dotnet publish "src/Esteira.Api/Esteira.Api.csproj" -c $BUILD_CONFIGURATION --no-restore -o /app/publish /p:UseAppHost=false


# ai aqui ele pega o preparo da base, copia para dentro dele
# os arquivos de /app/publish gerado pelo publish

# ai ele fala que o dotnet vai executar a dll da api
FROM base AS final
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Esteira.Api.dll"]
