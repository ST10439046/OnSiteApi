# Use official .NET SDK image for build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY *.csproj ./
RUN dotnet restore
COPY . ./
RUN dotnet publish -c Release -o /app/publish

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
# Expose port (default 80)
EXPOSE 80
ENV ASPNETCORE_URLS=http://+:80
# Load environment variables from .env if present (docker automatically loads .env)
ENTRYPOINT ["dotnet", "OnSiteApi.dll"]
