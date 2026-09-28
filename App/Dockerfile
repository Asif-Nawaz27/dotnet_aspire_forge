# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore from project files alone first, so the (slow) restore layer is cached until a
# dependency actually changes rather than on every source edit.
COPY Directory.Packages.props ./
COPY Feature/App.Domain/App.Domain.csproj Feature/App.Domain/
COPY Feature/App.Application/App.Application.csproj Feature/App.Application/
COPY Feature/App.Infrastructure/App.Infrastructure.csproj Feature/App.Infrastructure/
COPY Feature/App.ServiceDefaults/App.ServiceDefaults.csproj Feature/App.ServiceDefaults/
COPY Feature/App.Api/App.Api.csproj Feature/App.Api/
RUN dotnet restore Feature/App.Api/App.Api.csproj

COPY Feature/ Feature/
RUN dotnet publish Feature/App.Api/App.Api.csproj -c Release -o /app --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_gcServer=1
EXPOSE 8080

COPY --from=build /app .

# The base image ships a non-root 'app' user; never run the API as root.
USER $APP_UID

ENTRYPOINT ["dotnet", "App.Api.dll"]
