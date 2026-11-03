FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/DgiiHelper.Api/DgiiHelper.Api.csproj src/DgiiHelper.Api/
RUN dotnet restore src/DgiiHelper.Api/DgiiHelper.Api.csproj
COPY src/ src/
RUN dotnet publish src/DgiiHelper.Api -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
# La fecha de vencimiento se calcula con UTC-4 fijo, así que no hace falta tzdata.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "DgiiHelper.Api.dll"]
