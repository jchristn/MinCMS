# MinCMS Docker Factory Defaults

This directory contains the factory-default Docker deployment state restored by:

- `docker/factory/reset.bat`
- `docker/factory/reset.sh`

Factory-managed content:

- `mincms_server_config/mincms.json` restores `docker/server/mincms.json`
- `mincms_dashboard_config/entrypoint.sh` restores `docker/dashboard/entrypoint.sh`
- `mincms_server_logs` restores `docker/server/logs`
- `mincms_dashboard_logs` restores `docker/dashboard/logs`

Notes:

- MinCMS stores collections, file metadata, and file content in the configured S3-compatible bucket, not in local Docker volumes.
- Factory reset only affects the local Docker deployment state. It does not delete or modify data already stored in S3.
- The restored `mincms.json` is the repository factory template with placeholder S3 credentials and the default `mincmsadmin` API key.
- The reset scripts stop the deployment, restore the factory files, clear the local log directories, and leave the stack stopped so it can be started cleanly.
