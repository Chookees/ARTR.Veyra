# Runbook: Restart

1. Drain traffic at the edge load balancer if present.
2. Stop the service (`Stop-Service` / `systemctl stop artr-veyra`).
3. Confirm process exit.
4. Start the service.
5. Verify `/_veyra/health/ready` returns 200.
