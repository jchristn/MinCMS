#
#
# Build stage
#
#

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore dependencies
COPY MinCms.Core/MinCms.Core.csproj MinCms.Core/
COPY MinCms.Server/MinCms.Server.csproj MinCms.Server/
RUN dotnet restore MinCms.Server/MinCms.Server.csproj

# Build and publish
COPY . .
RUN dotnet publish MinCms.Server/MinCms.Server.csproj -c Release -o /app/publish

#
#
# Runtime stage
#
#

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

RUN apt-get update && apt-get install -y --no-install-recommends \
    curl wget iputils-ping dnsutils vim \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8100
ENTRYPOINT ["dotnet", "MinCms.Server.dll"]
