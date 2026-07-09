# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["PremierClic.Api.csproj", "./"]
RUN dotnet restore "./PremierClic.Api.csproj"
COPY . ./
RUN dotnet publish "PremierClic.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish ./
EXPOSE 80
ENTRYPOINT ["dotnet", "PremierClic.Api.dll"]
