# MinCMS Docker Factory Defaults

This directory contains the factory-default Docker deployment state restored by:

- `docker/factory/reset.bat`
- `docker/factory/reset.sh`

Factory-managed content:

- `mincms_server_config/mincms.json` restores `docker/server/mincms.json`
- `mincms_dashboard_config/entrypoint.sh` restores `dashboard/docker-entrypoint.sh`
- `mincms_server_logs` restores `docker/server/logs`
- `mincms_dashboard_logs` restores `docker/dashboard/logs`
- `less3_config/system.json` restores `docker/less3/system.json`
- `less3_database` restores `docker/less3/db`
- `less3_logs` restores `docker/less3/logs`
- `less3_temp` restores `docker/less3/temp`
- `less3_disk` restores `docker/less3/disk`

Notes:

- The Docker deployment is factory-configured to use the bundled Less3 service as the MinCMS S3-compatible backend.
- `docker/compose.yaml` bootstraps the Less3 runtime state at container start and copies `less3_database/less3.db` into `docker/less3/db` whenever that runtime directory is empty.
- Factory reset clears the local Less3 database, object storage, temp files, and logs, then restores the default Less3 database and MinCMS deployment files.
- The restored MinCMS S3 settings use the Less3 Docker defaults: access key `default`, secret key `default`, bucket `default`, region `us-west-1`, path-style URLs, and `http://less3:8000` for in-network access.
- The reset scripts stop the deployment, restore the factory files, clear the local runtime directories, and leave the stack stopped so it can be started cleanly.
