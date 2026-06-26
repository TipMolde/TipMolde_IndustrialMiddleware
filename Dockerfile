FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["TipMolde.IndustrialMiddleware/TipMolde.IndustrialMiddleware.csproj", "TipMolde.IndustrialMiddleware/"]
RUN dotnet restore "TipMolde.IndustrialMiddleware/TipMolde.IndustrialMiddleware.csproj"

COPY ["TipMolde.IndustrialMiddleware/", "TipMolde.IndustrialMiddleware/"]

WORKDIR "/src/TipMolde.IndustrialMiddleware"
RUN dotnet publish "TipMolde.IndustrialMiddleware.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "TipMolde.IndustrialMiddleware.dll"]
