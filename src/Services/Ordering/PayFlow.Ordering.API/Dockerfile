FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app

COPY src/BuildingBlocks/PayFlow.SharedKernel/*.csproj src/BuildingBlocks/PayFlow.SharedKernel/
COPY src/Services/Ordering/PayFlow.Ordering.API/*.csproj src/Services/Ordering/PayFlow.Ordering.API/
RUN dotnet restore src/Services/Ordering/PayFlow.Ordering.API/PayFlow.Ordering.API.csproj

COPY src/BuildingBlocks/PayFlow.SharedKernel/ src/BuildingBlocks/PayFlow.SharedKernel/
COPY src/Services/Ordering/PayFlow.Ordering.API/ src/Services/Ordering/PayFlow.Ordering.API/
WORKDIR /app/src/Services/Ordering/PayFlow.Ordering.API
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "PayFlow.Ordering.API.dll"]
