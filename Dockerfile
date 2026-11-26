# ---------- Stage 1: build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy only the project file first, so package restore is cached
COPY Marketplace.API/Marketplace.API.csproj Marketplace.API/
RUN dotnet restore Marketplace.API/Marketplace.API.csproj

# Now copy everything else and build
COPY . .
RUN dotnet publish Marketplace.API/Marketplace.API.csproj -c Release -o /app/publish /p:UseAppHost=false

# ---------- Stage 2: run ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Render tells the app which port to use through the PORT variable (default 10000)
CMD ["sh", "-c", "ASPNETCORE_URLS=http://+:${PORT:-10000} dotnet Marketplace.API.dll"]