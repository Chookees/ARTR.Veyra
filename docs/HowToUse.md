# How to use ARTR Veyra

## Install the SDK

Install the .NET 10 SDK from https://dotnet.microsoft.com/download.

## Configure

Copy `config/veyra.example.json` and adjust `ReverseProxy` routes/clusters and `ARTR:Veyra` settings.
Environment variables use the `ARTR_VEYRA_` prefix (nested keys use `__`).

## Run

```bash
dotnet run --project src/ARTR.Veyra.Host --configuration Release --urls http://127.0.0.1:5080
```

Or use `build/RunDemo.ps1` / `build/RunDemo.sh` with the sample upstreams.

## Authenticate

- API key: send header `X-Api-Key` with a key whose SHA-256 hex hash is listed in config.
- JWT: configure Authority or SigningKeySecretName and send `Authorization: Bearer …`.

## Observe

- Logs: structured console logging
- Traces/metrics: enable OTLP and/or Prometheus under `ARTR:Veyra:Observability`
- Correlation: `X-Correlation-ID`

## Operate

See [operations](operations/README.md) for service install, health probes, and runbooks.
