FROM mcr.microsoft.com/dotnet/aspnet:10.0.12-resolute-chiseled@sha256:99a0fe6f3e933a3d170aa38d8be4237125b72c644a9229889ee842628cbaf845 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 9001

FROM mcr.microsoft.com/dotnet/sdk:10.0.401-resolute@sha256:d5355a3715a4a2cd4c63425bb6d41807116672550e12ab48b9be9cf22dbe40af AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY [".editorconfig", "."]
COPY ["Directory.Build.props", "."]
COPY ["src/UnfoldedCircle.AdbTv/UnfoldedCircle.AdbTv.csproj", "UnfoldedCircle.AdbTv/"]
COPY ["tools/appnames/appnames.dex", "/tools/appnames/appnames.dex"]
RUN dotnet restore "UnfoldedCircle.AdbTv/UnfoldedCircle.AdbTv.csproj" --locked-mode
COPY ./src .

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
WORKDIR "/src/UnfoldedCircle.AdbTv"
RUN dotnet publish "UnfoldedCircle.AdbTv.csproj" -c "$BUILD_CONFIGURATION" \
    -o /app/publish /p:PublishTrimmed=false /p:PublishSingleFile=true /p:UseAppHost=true
RUN touch /app/publish/configured_entities.json && \
    chown "$APP_UID" /app/publish/configured_entities.json && \
    chmod 644 /app/publish/configured_entities.json

FROM base AS final
USER $APP_UID
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["./driver"]