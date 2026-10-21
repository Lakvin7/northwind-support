# Multi-stage Dockerfile for SupportDesk API
# Stage 1: Build & Publish using official .NET 10 SDK
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy configuration and project files for optimal layer caching
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY apps/api/src/SupportDesk.Domain/SupportDesk.Domain.csproj apps/api/src/SupportDesk.Domain/
COPY apps/api/src/SupportDesk.Application/SupportDesk.Application.csproj apps/api/src/SupportDesk.Application/
COPY apps/api/src/SupportDesk.Infrastructure/SupportDesk.Infrastructure.csproj apps/api/src/SupportDesk.Infrastructure/
COPY apps/api/src/SupportDesk.Presentation/SupportDesk.Presentation.csproj apps/api/src/SupportDesk.Presentation/

# Restore dependencies for the Presentation application and its dependencies
RUN dotnet restore apps/api/src/SupportDesk.Presentation/SupportDesk.Presentation.csproj

# Copy remaining application source code
COPY apps/api/src/ apps/api/src/

# Publish the application in Release mode without re-restoring dependencies
RUN dotnet publish apps/api/src/SupportDesk.Presentation/SupportDesk.Presentation.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# Stage 2: Runtime image using official ASP.NET Core 10 runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Configure non-root user and HTTP port 8080
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080

USER $APP_UID

# Copy published application artifacts
COPY --from=build --chown=$APP_UID:$APP_UID /app/publish .

ENTRYPOINT ["dotnet", "SupportDesk.Presentation.dll"]
