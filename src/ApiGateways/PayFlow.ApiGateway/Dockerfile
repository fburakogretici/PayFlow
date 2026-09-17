FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app

COPY src/BuildingBlocks/PayFlow.SharedKernel/*.csproj src/BuildingBlocks/PayFlow.SharedKernel/
COPY src/ApiGateways/PayFlow.ApiGateway/*.csproj src/ApiGateways/PayFlow.ApiGateway/
RUN dotnet restore src/ApiGateways/PayFlow.ApiGateway/PayFlow.ApiGateway.csproj

COPY src/BuildingBlocks/PayFlow.SharedKernel/ src/BuildingBlocks/PayFlow.SharedKernel/
COPY src/ApiGateways/PayFlow.ApiGateway/ src/ApiGateways/PayFlow.ApiGateway/
WORKDIR /app/src/ApiGateways/PayFlow.ApiGateway
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "PayFlow.ApiGateway.dll"]
