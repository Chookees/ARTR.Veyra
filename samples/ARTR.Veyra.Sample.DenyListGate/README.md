# Sample: Deny-list request gate

DI-only `IVeyraRequestGate` extension (no dynamic DLL loading).

## Register

In your host composition root:

```csharp
using ARTR.Veyra.Core.Extensions;
using ARTR.Veyra.Sample.DenyListGate;

builder.Services.AddSingleton<IVeyraRequestGate, DenyListRequestGate>();
```

Config:

```json
"Features": {
  "Enabled": [ "deny-list" ]
}
```

When `Features.Enabled` is empty, all DI-registered gates run. When non-empty, only listed `FeatureId` values run.

## Behavior

Requests to `/a/blocked` or `/b/blocked` receive HTTP 403 problem+json.
