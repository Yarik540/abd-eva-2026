FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copiar toda la solución
COPY . .

# Restaurar desde la solución completa
RUN dotnet restore "abd-eva-2026/abd-eva-2026.csproj"

# Publicar
RUN dotnet publish "abd-eva-2026/abd-eva-2026.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "abd-eva-2026.dll"]