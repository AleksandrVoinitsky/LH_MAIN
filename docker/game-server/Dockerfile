FROM ubuntu:24.04

RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates libatomic1 libpulse0 libstdc++6 curl \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY Builds/GameServer/LinuxHeadless/ /app/
COPY docker/game-server/entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh /app/LH.Main.GameServer.x86_64

EXPOSE 8081/tcp
EXPOSE 7770/udp

ENTRYPOINT ["/entrypoint.sh"]
