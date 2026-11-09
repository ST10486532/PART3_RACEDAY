# syntax=docker/dockerfile:1
# RaceDay MVC web application (src/RaceDay.Web)
#
# Build:  docker build -t raceday-web .
# Run:    docker run --rm -p 8080:8080 -e Api__BaseUrl=http://host.docker.internal:5080 raceday-web

# ---------- build stage ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore first, on its own layer, so NuGet packages are cached until the project file changes.
COPY src/RaceDay.Web/RaceDay.Web.csproj src/RaceDay.Web/
RUN dotnet restore src/RaceDay.Web/RaceDay.Web.csproj

COPY src/RaceDay.Web/ src/RaceDay.Web/
RUN dotnet publish src/RaceDay.Web/RaceDay.Web.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---------- runtime stage (small image, no SDK) ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true \
    Api__BaseUrl=http://host.docker.internal:5080

COPY --from=build /app/publish .

# Run as the non-root user that ships with the .NET 8 images.
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "RaceDay.Web.dll"]
