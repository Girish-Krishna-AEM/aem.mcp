# Local Docker Compose Test Results: Unit 3

## Full Verification — PASSED ✅

Docker Desktop's engine was brought up in this environment and the full stack was built, run, and verified end-to-end. **Four real bugs were found and fixed in the process** (see below) — none were caught by `dotnet build`/`dotnet test` alone, since they only manifest when actually building/running the Docker images.

| Check | Result |
|---|---|
| `docker compose config --quiet` | ✅ Pass |
| `docker compose build` (both images) | ✅ Pass (after fixes below) |
| `docker compose up -d` | ✅ Pass — both containers reach `Up (healthy)` |
| `GET /health` on `lightning-mcp-server` (port 8000) | ✅ `{"status":"healthy","timestamp":"..."}` |
| `GET /health` on `coordinator-agent` (port 8001) | ✅ `{"status":"healthy","timestamp":"...","mcp_server":"reachable"}` |
| `POST /query` — Strike intent ("Is there lightning near Austin, TX?") | ✅ 200, `strikeCount: 9`, full strike list, summary |
| `POST /query` — Weather intent ("What is the weather forecast for Dallas?") | ✅ 200, forecast entry, summary |
| `POST /query` — Sensor intent ("Show diagnostics for sensor-001") | ✅ 200, all 8 diagnostic metrics, summary |
| `POST /query` — Informer intent ("Is the horn in zone-a powered on?") | ✅ 200, status/power/zone/deviceType, summary |
| `POST /query` — missing location ("Is there lightning?") | ✅ 400 with the specific clarification message |
| `docker compose logs` | ✅ Structured JSON from both services, including the Coordinator's outbound HTTP call trace to the MCP Server |
| `docker compose down` | ✅ Clean teardown |

## Bugs Found and Fixed During This Verification

1. **Invalid base image tags** — both Dockerfiles used `mcr.microsoft.com/dotnet:10.0-sdk` / `mcr.microsoft.com/dotnet:10.0-aspnet`, which don't exist under that naming scheme. Fixed to `mcr.microsoft.com/dotnet/sdk:10.0` and `mcr.microsoft.com/dotnet/aspnet:10.0` in both `unit-1/src/LightningMcpServer/Dockerfile` and `unit-2/src/CoordinatorAgent/Dockerfile`.
2. **Wrong `docker-compose.yml` build context** — Unit 3's compose file set `context: ./unit-1` / `./unit-2`, but both Dockerfiles' `COPY` instructions use paths relative to the **repo root** (e.g., `COPY unit-1/src/...`). Fixed both services' `context` to `.` with the `dockerfile` path adjusted accordingly.
3. **Broken multi-project restore in Unit 1's Dockerfile** — `RUN cd ../LightningMcpServer && dotnet restore` tried to `cd` up from `/src` (each `RUN` starts fresh at `WORKDIR /src`, so `LightningMcpServer` is a sibling directory, not reachable via `../`). Fixed to `RUN cd LightningMcpServer && dotnet restore`.
4. **Missing `.dockerignore`** — without one, the full build context included host `bin/`/`obj/` directories containing a Windows-specific `project.assets.json` (referencing a Visual Studio NuGet fallback path). Copying these into the Linux build stage overwrote the container's own freshly-restored assets file, breaking `dotnet publish --no-restore` with `NuGet.Packaging.Core.PackagingException: Unable to find fallback package folder 'C:\Program Files (x86)\...'`. Added a root-level `.dockerignore` excluding `**/bin/`, `**/obj/`, and other local-only paths.
5. **Missing `curl` in the runtime image** — both Dockerfiles' `HEALTHCHECK CMD curl ...` failed with `exec: "curl": executable file not found in $PATH`, since `mcr.microsoft.com/dotnet/aspnet:10.0` doesn't include `curl` by default (this was flagged as a risk in the prior version of this document, before Docker was available to actually test it). Fixed by adding `RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*` to the runtime stage of both Dockerfiles.

## Environment Note

This machine already had another local process bound to host port 8000 (unrelated to this project), so verification was temporarily run with ports remapped to 18000/18001 via an in-place edit of `docker-compose.yml`, then reverted back to the committed 8000/8001 mapping once verification passed. The committed `docker-compose.yml` is unchanged in its actual design (same port mapping as originally specified) — only the five bug fixes above are new.

## Conclusion

Unit 3's orchestration artifacts are now fully verified end-to-end, including real cross-container HTTP traffic (Coordinator Agent → Lightning MCP Server). The stack is ready for EC2 deployment per `DEPLOYMENT.md` / `unit-3/docs/ec2-deployment-guide.md`.
