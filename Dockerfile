# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["ShiftingGuru.csproj", "./"]
RUN dotnet restore "ShiftingGuru.csproj"

COPY . .
RUN dotnet publish "ShiftingGuru.csproj" \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://0.0.0.0:8080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

ENTRYPOINT ["dotnet", "ShiftingGuru.dll"]
