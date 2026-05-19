# Backend Dockerfile for Render
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["PriceVision.Api.csproj", "./"]
RUN dotnet restore "PriceVision.Api.csproj"

COPY . .
RUN dotnet publish "PriceVision.Api.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish ./
COPY ["pricevision.db", "./"]
COPY ["Artifacts", "./Artifacts"]

EXPOSE 5054
ENV ASPNETCORE_URLS=http://0.0.0.0:5054
ENTRYPOINT ["sh", "-c", "ASPNETCORE_URLS=http://0.0.0.0:${PORT:-5054} dotnet PriceVision.Api.dll"]
