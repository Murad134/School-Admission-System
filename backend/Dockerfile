FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /app

# Copy solution and project definitions for layer caching
COPY SchoolAdmissionSystem.sln ./
COPY src/SchoolAdmissionSystem.Api/SchoolAdmissionSystem.Api.csproj src/SchoolAdmissionSystem.Api/
COPY tests/SchoolAdmissionSystem.Tests/SchoolAdmissionSystem.Tests.csproj tests/SchoolAdmissionSystem.Tests/

# Restore dependencies
RUN dotnet restore src/SchoolAdmissionSystem.Api/SchoolAdmissionSystem.Api.csproj

# Copy all source files
COPY . .

# Build and publish release output
RUN dotnet publish src/SchoolAdmissionSystem.Api/SchoolAdmissionSystem.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0

WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "SchoolAdmissionSystem.Api.dll"]
