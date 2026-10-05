FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["Directory.Build.props", "./"]
COPY ["global.json", "./"]
COPY ["src/TentacionSana.Domain/TentacionSana.Domain.csproj", "src/TentacionSana.Domain/"]
COPY ["src/TentacionSana.Application/TentacionSana.Application.csproj", "src/TentacionSana.Application/"]
COPY ["src/TentacionSana.Infrastructure/TentacionSana.Infrastructure.csproj", "src/TentacionSana.Infrastructure/"]
COPY ["src/TentacionSana.Web/TentacionSana.Web.csproj", "src/TentacionSana.Web/"]

RUN dotnet restore "src/TentacionSana.Web/TentacionSana.Web.csproj"

COPY . .
WORKDIR "/src/src/TentacionSana.Web"
RUN dotnet publish "TentacionSana.Web.csproj" --configuration Release --no-restore --output /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Render entrega las solicitudes al puerto 10000 por defecto.
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_URLS=http://+:10000 \
    DataProtection__KeysPath=/tmp/tentacion-sana-data-protection-keys \
    ForwardedHeaders__Enabled=true

EXPOSE 10000

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "TentacionSana.Web.dll"]
