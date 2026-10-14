FROM mcr.microsoft.com/dotnet/aspnet:11.0.0-rc.2-resolute-chiseled@sha256:f502def1917cec1c8e401a65b5866704dd51ca99857f139ff11bf7f7685d7fa0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 9001

FROM mcr.microsoft.com/dotnet/sdk:11.0.100-rc.2-resolute@sha256:bfabf51e74c5a7cf2fc4eac852145db86f4867d475257d809518d6a2b0fe71e1 AS build
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