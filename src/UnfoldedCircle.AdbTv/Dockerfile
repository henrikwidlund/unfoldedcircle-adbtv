FROM mcr.microsoft.com/dotnet/aspnet:11.0.0-rc.1-resolute-chiseled@sha256:af930df2f1e6da76666d818c5922534cc7f2a4077adfae27e7c81c28a6406d29 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 9001

FROM mcr.microsoft.com/dotnet/sdk:11.0.100-rc.1-resolute@sha256:78559d13d8c810bfe87f329168739ae2ee31f402b1e2c66353d3ff9744d68520 AS build
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