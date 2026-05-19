# Dockerfile for PriceVision backend (Render / local Docker)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy only project files first to leverage build cache
COPY ["PriceVision.Api/PriceVision.Api.csproj", "PriceVision.Api/"]
COPY ["PriceVision.Application/PriceVision.Application.csproj", "PriceVision.Application/"]
COPY ["PriceVision.Domain/PriceVision.Domain.csproj", "PriceVision.Domain/"]
COPY ["PriceVision.Infrastructure/PriceVision.Infrastructure.csproj", "PriceVision.Infrastructure/"]

RUN dotnet restore "PriceVision.Api/PriceVision.Api.csproj"

# Copy all source files
COPY . .
RUN dotnet publish "PriceVision.Api/PriceVision.Api.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish ./
COPY ["PriceVision.Api/pricevision.db", "./"]
COPY ["PriceVision.Api/Artifacts", "./Artifacts"]

EXPOSE 5054
ENV ASPNETCORE_URLS=http://0.0.0.0:5054
ENTRYPOINT ["sh", "-c", "ASPNETCORE_URLS=http://0.0.0.0:${PORT:-5054} dotnet PriceVision.Api.dll"]
