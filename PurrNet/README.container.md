# PurrNet — Container Hosting (server 192.168.0.180 already runs MariaDB)

## Quick start — host DB (192.168.0.180)
```bash
cp .env.example .env   # set GITHUB_CLIENT_ID/SECRET and MARIADB_PASSWORD to match host's purrnet user
# .env defaults to MARIADB_HOST=host.docker.internal (resolves to 192.168.0.180 via extra_hosts)
# No need to change to localhost — inside container localhost = container itself!
docker compose up -d --build
curl http://192.168.0.180:8080/health   # should return {"status":"Healthy","database":"MariaDB"}
docker compose logs -f purrnet
```

`purr` CLI talks to `http://192.168.0.180:8080/api/v1/packages` (or `http://localhost:8080` when testing locally).

MariaDB on the host must listen on 192.168.0.180 or 0.0.0.0 (not just 127.0.0.1). Check:
```ini
# /etc/my.cnf.d/server.cnf or /etc/mysql/my.cnf
[mysqld]
bind-address = 0.0.0.0
```
and allow `purrnet@'%'` or `purrnet@'172.%'` (docker bridge). Then `sudo systemctl restart mariadb`.

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

## Run on a specific drive (HDD slow → SSD)

Your HDD is slow — put the DB (and optionally the whole Docker data) on the fast drive:

**Option A — just the DB (recommended, via compose):**
```bash
# in .env, point to a directory on your fast drive
DB_DATA_HOST_PATH=/mnt/ssd/purrnet-db   # Linux example
# DB_DATA_HOST_PATH=E:/purrnet-db      # Windows example (use /mnt/e/... in WSL)
mkdir -p /mnt/ssd/purrnet-db
# if you have existing data in the named volume, copy it:
# docker run --rm -v purrnet_db_data:/from -v /mnt/ssd/purrnet-db:/to alpine sh -c "cp -a /from/* /to/"
docker compose up -d --build
```
Compose uses `${DB_DATA_HOST_PATH:-db_data}:/var/lib/mysql` — if `DB_DATA_HOST_PATH` is set it's a bind mount to that drive, otherwise it stays a named volume `db_data`.

**Option B — whole Docker (images + volumes) on fast drive:**
```json
// /etc/docker/daemon.json
{ "data-root": "/mnt/ssd/docker" }
```
Then `sudo systemctl restart docker`. This moves everything.

## Env overrides

All secrets are env-driven, never baked into the image:
- `MARIADB_CONNECTION_STRING` (preferred) or individual `MARIADB_*` vars in compose
- `GITHUB_CLIENT_ID` / `GITHUB_CLIENT_SECRET` — required for login
- `DB_DATA_HOST_PATH` — host path for MariaDB data when using `--profile internal-db` (leave empty for default named volume)

## Troubleshooting

**Port / DB not reachable from container (your error)**
- Inside container `Server=localhost` means the container itself, NOT 192.168.0.180. Compose now defaults to `host.docker.internal` (via `extra_hosts: host-gateway`) and `Program.cs` auto-rewrites any `Server=localhost` to `host.docker.internal` when `/.dockerenv` exists.
- If you set `MARIADB_HOST=localhost` it will still be rewritten — just leave it as `host.docker.internal` or `192.168.0.180`.
- Test from inside container: `docker exec -it purrnet-purrnet-1 bash -c "getent hosts host.docker.internal; nc -zv host.docker.internal 3306; nc -zv 192.168.0.180 3306"`
- If that fails with `Connection refused`, MariaDB is still on `127.0.0.1` only — change to `0.0.0.0` as above.
- Alternative (if you can't change bind-address): use host network `docker-compose.host-network.yml` or add `network_mode: host` to `purrnet` and set `Server=127.0.0.1`.

**Cold boot**

- `docker compose ps` — `purrnet` should be `healthy`
- If logs show `MariaDB not ready — retrying`, that's expected for ~30s after power loss
- App no longer segfaults on mismatched runtime — Dockerfile now pins `sdk:10.0` + `aspnet:10.0` consistently
