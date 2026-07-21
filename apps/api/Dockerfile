FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY apps/api/TechYouthBpm.slnx ./
COPY apps/api/src/TechYouthBpm.Api/TechYouthBpm.Api.csproj src/TechYouthBpm.Api/
COPY apps/api/src/TechYouthBpm.Application/TechYouthBpm.Application.csproj src/TechYouthBpm.Application/
COPY apps/api/src/TechYouthBpm.Domain/TechYouthBpm.Domain.csproj src/TechYouthBpm.Domain/
COPY apps/api/src/TechYouthBpm.Infrastructure/TechYouthBpm.Infrastructure.csproj src/TechYouthBpm.Infrastructure/
RUN dotnet restore src/TechYouthBpm.Api/TechYouthBpm.Api.csproj

COPY apps/api/src ./src
RUN dotnet publish src/TechYouthBpm.Api/TechYouthBpm.Api.csproj --configuration Release --output /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

RUN mkdir -p /data && chown -R app:app /app /data
COPY --chown=app:app --from=build /app/publish ./
USER app
ENTRYPOINT ["dotnet", "TechYouthBpm.Api.dll"]
