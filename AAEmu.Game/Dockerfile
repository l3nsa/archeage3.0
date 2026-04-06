FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY AAEmu.Commons/AAEmu.Commons.csproj AAEmu.Commons/
COPY AAEmu.Game/AAEmu.Game.csproj AAEmu.Game/
RUN dotnet restore AAEmu.Game/AAEmu.Game.csproj
COPY AAEmu.Commons/ AAEmu.Commons/
COPY AAEmu.Game/ AAEmu.Game/
RUN dotnet publish AAEmu.Game/AAEmu.Game.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/runtime:6.0
WORKDIR /app
COPY --from=build /app .
EXPOSE 1239 1250
ENTRYPOINT ["dotnet", "AAEmu.Game.dll"]
