FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY server/Directory.Build.props server/
COPY server/src/LH.Main.Contracts/LH.Main.Contracts.csproj server/src/LH.Main.Contracts/
COPY server/src/LH.Main.Backend.Api/LH.Main.Backend.Api.csproj server/src/LH.Main.Backend.Api/
RUN dotnet restore server/src/LH.Main.Backend.Api/LH.Main.Backend.Api.csproj

COPY server/ server/
RUN dotnet publish server/src/LH.Main.Backend.Api/LH.Main.Backend.Api.csproj --configuration Release --output /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "LH.Main.Backend.Api.dll"]
