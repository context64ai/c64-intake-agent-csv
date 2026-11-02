ARG VERSION="0.0.0"

FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build


# Install additional tools
RUN apt-get update \
    && apt-get install -y --no-install-recommends bash ca-certificates curl dnsutils htop iftop iotop iputils-ping jq nano netcat-openbsd openssl unzip wget \
    && apt-get autoremove -yqq --purge \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/* /tmp/* /var/tmp/*

# Install semver tool
RUN curl -o /usr/local/bin/semver https://raw.githubusercontent.com/fsaintjacques/semver-tool/master/src/semver \
    && chmod +x /usr/local/bin/semver \
    && export PATH=$PATH:/usr/local/bin

# Workaround to add tools to the path
ENV PATH=${PATH}:/root/.dotnet/tools

# Install dotnet-ef
RUN dotnet tool install --global dotnet-ef --version 9.0.11

# Install nuget-license
RUN dotnet tool install --global nuget-license

SHELL ["/bin/bash", "-c"]
WORKDIR /app

COPY ./src ./src
COPY DCH.CsvIntakeAgent.sln NuGet.Config Directory.Build.props ./

RUN dotnet restore "src/DCH.CsvIntakeAgent/DCH.CsvIntakeAgent.csproj"
RUN dotnet build "src/DCH.CsvIntakeAgent/DCH.CsvIntakeAgent.csproj" --no-restore -c Release -o out
RUN dotnet publish "src/DCH.CsvIntakeAgent/DCH.CsvIntakeAgent.csproj" --no-restore -c Release -o publish /p:UseAppHost=false

# Find list of projects except test projects.
RUN projects=$(find /app/src/*/*.csproj -name '*.csproj' ! -path "/app/src/*.Tests/*.csproj" -exec echo -n '"{}", ' \;)  \
    && projects=${projects::-2} && echo "[${projects}]" > projects-filter.json
RUN echo '["DCH.*"]' > ignored-packages.json
RUN nuget-license --json-input projects-filter.json --ignored-packages ignored-packages.json --output Json > LICENSES.json

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble

ENV PATH="/usr/local/bin:${PATH}"
# Install additional tools
RUN apt-get update \
    && apt-get install -y --no-install-recommends bash ca-certificates curl dnsutils htop iftop iotop iputils-ping jq nano netcat-openbsd sudo wget \
    && apt-get autoremove -yqq --purge \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/* /tmp/* /var/tmp/*

# Add health check assuming that service uses HTTP port 8080
HEALTHCHECK CMD curl --fail http://localhost:8080 || exit 1

# Use non-root 'app' user
USER $APP_UID

ARG VERSION

WORKDIR /app

COPY --chown=app --from=build /app/publish .
COPY --chown=app --from=build /app/LICENSES.json .
COPY --chown=app --chmod=544 ./config/entrypoint.sh ./entrypoint.sh

# Add version file
RUN echo $VERSION > ./VERSION

ENTRYPOINT ["/app/entrypoint.sh"]