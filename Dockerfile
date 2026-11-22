FROM node:24-alpine AS web
WORKDIR /web
COPY web/package*.json ./
RUN npm ci
COPY web/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api
WORKDIR /src
COPY api/ ./
RUN dotnet publish Tapp.Api -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=api /out ./
COPY --from=web /web/build ./wwwroot
# SQLite file and product pictures live on this volume.
ENV ConnectionStrings__Default="Data Source=/data/tapp.db" Pictures__Path=/data/pictures ASPNETCORE_URLS=http://+:8080
VOLUME /data
EXPOSE 8080
ENTRYPOINT ["dotnet", "Tapp.Api.dll"]
