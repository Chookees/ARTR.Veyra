# Architecture overview

ARTR Veyra is a process-hosted API gateway. Clients call the data plane; YARP forwards to configured upstreams after authentication, authorization, and rate limiting.

See ADRs:

- [0001 YARP](adr/0001-yarp-reverse-proxy.md)
- [0002 No containers](adr/0002-no-containers.md)
- [0003 Rate limiting](adr/0003-local-rate-limiting.md)
- [0004 Secrets](adr/0004-secret-resolution.md)
- [0005 Prometheus beta](adr/0005-prometheus-exporter-beta.md)
- [0006 Layering](adr/0006-layered-boundaries.md)
- [0007 Admin listener isolation](adr/0007-admin-listener-isolation.md)
- [0008 Configuration activation](adr/0008-configuration-activation.md)
