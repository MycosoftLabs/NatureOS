# Multi-stage Dockerfile for NatureOS Core API
# Base image for runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Create a non-root user
RUN adduser --disabled-password --gecos "" --uid 1001 appuser

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy solution file
COPY ["NatureOS.sln", "."]

# Copy project files
COPY ["src/core-api/NatureOS.CoreApi.csproj", "src/core-api/"]
COPY ["src/mindex/NatureOS.MINDEX.csproj", "src/mindex/"]
COPY ["src/mycorrhizae/NatureOS.Mycorrhizae.csproj", "src/mycorrhizae/"]

# Restore dependencies
RUN dotnet restore "src/core-api/NatureOS.CoreApi.csproj"

# Copy source code
COPY . .

# Build the application
WORKDIR "/src/src/core-api"
RUN dotnet build "NatureOS.CoreApi.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Publish stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "NatureOS.CoreApi.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Final stage
FROM base AS final
WORKDIR /app

# Copy published application
COPY --from=publish /app/publish .

# Switch to non-root user
USER appuser

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:8080/health || exit 1

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Labels for metadata
LABEL org.opencontainers.image.title="NatureOS Core API"
LABEL org.opencontainers.image.description="Cloud-native operating system for nature - Core API"
LABEL org.opencontainers.image.vendor="Mycosoft Labs"
LABEL org.opencontainers.image.source="https://github.com/MycosoftLabs/NatureOS"
LABEL org.opencontainers.image.licenses="MIT"

ENTRYPOINT ["dotnet", "NatureOS.CoreApi.dll"] 