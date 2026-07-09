FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY apps/api/*.csproj ./
RUN dotnet restore

COPY apps/api/ ./
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production
# ASPNETCORE_URLS is intentionally NOT set here — Docker ENV can't substitute a
# runtime-injected variable like Render's $PORT at build time. It's set instead in
# render.yaml, where Render's own env var interpolation resolves $PORT correctly.

ENTRYPOINT ["dotnet", "MidiKaval.Api.dll"]
