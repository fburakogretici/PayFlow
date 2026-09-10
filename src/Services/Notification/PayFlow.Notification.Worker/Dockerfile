FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app

COPY src/BuildingBlocks/PayFlow.SharedKernel/*.csproj src/BuildingBlocks/PayFlow.SharedKernel/
COPY src/Services/Notification/PayFlow.Notification.Worker/*.csproj src/Services/Notification/PayFlow.Notification.Worker/
RUN dotnet restore src/Services/Notification/PayFlow.Notification.Worker/PayFlow.Notification.Worker.csproj

COPY src/BuildingBlocks/PayFlow.SharedKernel/ src/BuildingBlocks/PayFlow.SharedKernel/
COPY src/Services/Notification/PayFlow.Notification.Worker/ src/Services/Notification/PayFlow.Notification.Worker/
WORKDIR /app/src/Services/Notification/PayFlow.Notification.Worker
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "PayFlow.Notification.Worker.dll"]
