# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
ARG TARGETARCH

# Install Node.js
RUN apk add --no-cache nodejs npm

COPY . /source

WORKDIR /source

RUN npm i
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet publish -a ${TARGETARCH/amd64/x64} --use-current-runtime --self-contained false -o /app

FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS development
# Install Node.js
RUN apk add --no-cache nodejs npm
COPY . /source
WORKDIR /source
RUN npm i
CMD dotnet watch run --no-launch-profile --non-interactive --no-hot-reload

FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS final
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENTRYPOINT ["dotnet", "ScriptureSearch.dll"]
