# ==========================================
# Build stage
# ==========================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY *.csproj ./

RUN dotnet restore

COPY . ./

RUN dotnet publish -c Release -o /app/publish


# ==========================================
# Runtime stage
# ==========================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

WORKDIR /app

COPY --from=build /app/publish .

# Render uses port 10000
EXPOSE 10000

ENV ASPNETCORE_URLS=http://0.0.0.0:10000

ENTRYPOINT ["dotnet", "OnSite Api.dll"]