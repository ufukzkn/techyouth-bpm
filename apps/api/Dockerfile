FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY apps/api/src ./src
RUN dotnet restore src/TechYouthBpm.Api/TechYouthBpm.Api.csproj
RUN dotnet publish src/TechYouthBpm.Api/TechYouthBpm.Api.csproj --configuration Release --output /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Development
EXPOSE 8080

COPY --from=build /app/publish ./
ENTRYPOINT ["dotnet", "TechYouthBpm.Api.dll"]
