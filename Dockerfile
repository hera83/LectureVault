# syntax=docker/dockerfile:1

# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first (leveraging Docker layer caching) — only the csproj is needed for this.
COPY web/web.csproj web/
RUN dotnet restore web/web.csproj

# Copy the rest of the source and publish.
COPY web/ web/
RUN dotnet publish web/web.csproj -c Release -o /app/publish --no-restore

# ---- Runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# The base ASP.NET Core 10 image already listens on port 8080 by default
# (ASPNETCORE_HTTP_PORTS=8080), which matches the port this project exposes.
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish .

# Pre-create the data/file folders so the mounted volumes have the right shape
# even before DatabaseInitializer runs on first start.
RUN mkdir -p App_dbs \
    App_files/avatars App_files/uploads App_files/exports App_files/temp App_files/lectures

EXPOSE 8080

ENTRYPOINT ["dotnet", "web.dll"]
