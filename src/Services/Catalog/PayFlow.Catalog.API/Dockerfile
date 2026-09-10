FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app

# Proje dosyalarını kopyala ve restore et (Docker layer caching optimizasyonu)
COPY src/BuildingBlocks/PayFlow.SharedKernel/*.csproj src/BuildingBlocks/PayFlow.SharedKernel/
COPY src/Services/Catalog/PayFlow.Catalog.API/*.csproj src/Services/Catalog/PayFlow.Catalog.API/
RUN dotnet restore src/Services/Catalog/PayFlow.Catalog.API/PayFlow.Catalog.API.csproj

# Kaynak kodları kopyala ve yayınla (publish)
COPY src/BuildingBlocks/PayFlow.SharedKernel/ src/BuildingBlocks/PayFlow.SharedKernel/
COPY src/Services/Catalog/PayFlow.Catalog.API/ src/Services/Catalog/PayFlow.Catalog.API/
WORKDIR /app/src/Services/Catalog/PayFlow.Catalog.API
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# Runtime imajı (Hafif ve güvenli)
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "PayFlow.Catalog.API.dll"]
