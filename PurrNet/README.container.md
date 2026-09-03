# PurrNet — Container Hosting

## Quick start
```bash
cp .env.example .env   # set GITHUB_CLIENT_ID/SECRET and MARIADB_PASSWORD to match host's purrnet user
# .env should have MARIADB_HOST=<yourserverip> (default) and correct MARIADB_DATABASE/USER/PASSWORD
docker compose up -d --build
curl http://<yourserverip>:8080/health   # should return {"status":"Healthy","database":"MariaDB"}
docker compose logs -f purrnet
```

`purr` CLI talks to `http://<yourserverip>:8080/api/v1/packages` (or `http://localhost:8080` when testing locally).

MariaDB on the host must listen on <yourserverip> (bind-address `0.0.0.0` or `<yourserverip>`) and allow user `purrnet@'%'` or `purrnet@'172.%'` (docker bridge).

## Dev self-contained (no host DB)

```bash
docker compose --profile internal-db up -d --build
curl http://localhost:8080/health
```

## Systemd (optional — if you want host-level auto-start on bare metal)

If you prefer systemd over `restart: unless-stopped`, use a unit that depends on docker:

```ini
# /etc/systemd/system/purrnet.service
[Unit]
Description=PurrNet (docker compose)
After=docker.service network-online.target
Requires=docker.service
Wants=network-online.target

[Service]
Type=oneshot
RemainAfterExit=yes
WorkingDirectory=/opt/purrnet
ExecStart=/usr/bin/docker compose up -d
ExecStop=/usr/bin/docker compose down
TimeoutStartSec=0

[Install]
WantedBy=multi-user.target
```

Then `systemctl enable --now purrnet`. The DB retry logic in `Program.cs` handles the 20-30s MariaDB warm-up after a power outage — no more segfault on boot.

## Env overrides

All secrets are env-driven, never baked into the image:
- `MARIADB_CONNECTION_STRING` (preferred) or individual `MARIADB_*` vars in compose
- `GITHUB_CLIENT_ID` / `GITHUB_CLIENT_SECRET` — required for login

## Troubleshooting cold boot

- `docker compose ps` — both `db` and `purrnet` should be `healthy`
- If `purrnet` logs show `MariaDB not ready — retrying`, that's expected for ~30s after power loss
- App no longer segfaults on mismatched runtime — Dockerfile now pins `sdk:10.0` + `aspnet:10.0` consistently
