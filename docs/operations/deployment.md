# Deployment

ARTR Veyra ships as a .NET 10 console host (`ARTR.Veyra.Host`). No containers are required.

## Publish

```bash
dotnet publish src/ARTR.Veyra.Host/ARTR.Veyra.Host.csproj -c Release -r <rid> -o ./publish
```

Supported RIDs: `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`. Scripts: `build/Publish.ps1`, `build/Publish.sh`.

## Configuration layout

Copy `config/veyra.example.json` to your environment and set `Urls`, `ReverseProxy`, and security options. Optional environment file for Linux: `/etc/artr-veyra/veyra.env`.

## Windows Service

```powershell
.\deploy\windows\Install-Service.ps1 -BinaryDirectory C:\path\to\publish
```

Update binaries:

```powershell
.\deploy\windows\Update-Service.ps1 -BinaryDirectory C:\path\to\new\publish
```

Remove:

```powershell
.\deploy\windows\Uninstall-Service.ps1
```

Legacy lowercase script names delegate to the canonical `Install-Service.ps1` / `Uninstall-Service.ps1`.

## Linux systemd

```bash
./deploy/linux/install.sh /opt/artr-veyra
# copy publish output to /opt/artr-veyra
sudo cp deploy/linux/systemd/artr-veyra.service /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable --now artr-veyra
```

Uninstall unit:

```bash
./deploy/linux/uninstall.sh
```

## Reverse proxy

Place nginx or Apache in front of the data-plane listener. Samples:

- `deploy/reverse-proxy/nginx.example.conf`
- `deploy/reverse-proxy/apache.example.conf`

Enable `ForwardedHeaders` in Veyra when terminating TLS or forwarding client IP at the edge.

## Admin listener isolation

Optional `Admin.ListenUrls` binds admin endpoints on dedicated ports. Restrict firewall rules so admin ports are reachable only from management networks.

## Health checks

Use `/_veyra/health/live`, `/_veyra/health/ready`, and `/_veyra/health/startup` for orchestrator probes. See [health](health.md).

## Post-deploy verification

1. `GET /_veyra/info` returns product identity
2. `GET /_veyra/config/summary` shows expected authentication and rate-limit flags
3. Proxied routes return expected upstream responses
4. Authentication denial returns 401 with Problem Details when enabled
