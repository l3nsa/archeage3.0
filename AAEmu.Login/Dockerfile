FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY AAEmu.Commons/AAEmu.Commons.csproj AAEmu.Commons/
COPY AAEmu.Login/AAEmu.Login.csproj AAEmu.Login/
RUN dotnet restore AAEmu.Login/AAEmu.Login.csproj
COPY AAEmu.Commons/ AAEmu.Commons/
COPY AAEmu.Login/ AAEmu.Login/
RUN dotnet publish AAEmu.Login/AAEmu.Login.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/runtime:6.0
WORKDIR /app
COPY --from=build /app .
EXPOSE 1237 1234
ENTRYPOINT ["dotnet", "AAEmu.Login.dll"]
