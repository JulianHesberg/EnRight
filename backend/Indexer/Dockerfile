# Use the official .NET 7 SDK image to build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy and restore
COPY *.csproj .
RUN dotnet restore

# Copy the rest of your source code
COPY . .
RUN dotnet publish -c Release -o /app

# Final stage: runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app .

# Default command
ENTRYPOINT ["dotnet", "Indexer.dll"]
