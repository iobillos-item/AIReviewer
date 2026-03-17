FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files first for layer caching
COPY AIReviewer.sln .
COPY AIReviewer.Domain/AIReviewer.Domain.csproj AIReviewer.Domain/
COPY AIReviewer.Application/AIReviewer.Application.csproj AIReviewer.Application/
COPY AIReviewer.Infrastructure/AIReviewer.Infrastructure.csproj AIReviewer.Infrastructure/
COPY AIReviewer.WebAPI/AIReviewer.WebAPI.csproj AIReviewer.WebAPI/
COPY AIReviewer.Application.Tests/AIReviewer.Application.Tests.csproj AIReviewer.Application.Tests/
RUN dotnet restore

# Copy everything and run tests
COPY . .
RUN dotnet test AIReviewer.Application.Tests --no-restore --configuration Release

# Publish
RUN dotnet publish AIReviewer.WebAPI -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "AIReviewer.WebAPI.dll"]
