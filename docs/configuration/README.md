# Configuration

Root section: `ARTR:Veyra`  
Proxy section: `ReverseProxy`  
Environment prefix: `ARTR_VEYRA_`

Example files live in `config/`. JSON Schema: `config/schemas/veyra.schema.json`.

Validation runs at startup via `IValidateOptions<VeyraOptions>`. Invalid configuration fails fast.
YARP transforms are checked against an allowlist before the host starts accepting traffic.
