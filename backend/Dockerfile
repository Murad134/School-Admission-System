FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

COPY src/SchoolAdmissionSystem.Api/SchoolAdmissionSystem.Api.csproj src/SchoolAdmissionSystem.Api/
RUN dotnet restore src/SchoolAdmissionSystem.Api/SchoolAdmissionSystem.Api.csproj

COPY . .
RUN dotnet publish src/SchoolAdmissionSystem.Api/SchoolAdmissionSystem.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "SchoolAdmissionSystem.Api.dll"]