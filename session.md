# Session Handoff — 2026-10-08T03:15:00Z

## Goal
Deploy the Lightning Detection MCP stack (Docker Compose, 2 services) onto Girish's existing repurposed EC2 instance via `unit-3/scripts/deploy-ec2.sh`, iterating on the script as real-world install errors surface.

## Next actions (literal, copy-pasteable)
1. On the instance, find which directory actually has the git clone: `ls -la ~/aem-mcp/.git 2>&1; ls -la /opt/lightning-mcp/.git 2>&1`
2. `cd` into whichever one has `.git` (almost certainly `~/aem-mcp`, NOT `/opt/lightning-mcp` — that one errored with "fatal: not a git repository")
3. `git pull origin main` — confirm it reports commit `18e4fa2` or later: `git log -1 --oneline unit-3/scripts/deploy-ec2.sh`
4. `sudo ./unit-3/scripts/deploy-ec2.sh`
5. Expect: google-chrome repo gets disabled (or skipped if already disabled from a prior run), docker already present, Compose plugin install falls back to downloading the official binary from GitHub releases into `/usr/local/lib/docker/cli-plugins/docker-compose` since `dnf install docker-compose-plugin` fails with "No match for argument" on this instance's repos
6. If it completes: `curl http://localhost:8020/health` and `curl http://localhost:8001/health`, then a test query: `curl -X POST http://localhost:8001/query -H "Content-Type: application/json" -d '{"query": "Is there lightning near Austin, TX?"}'`
7. Also test the real weather tools (expect likely 503/error if the internal staging weather API's `locations` dependency outage is still ongoing — not a deployment bug, see Risks below): `curl -X POST http://localhost:8001/query -H "Content-Type: application/json" -d '{"query": "What is the weather in Austin, TX?"}'`

## Context & Discovery
- Instance found via `aws ec2 describe-instances --profile qa --filters "Name=private-ip-address,Values=10.8.20.224"`: `i-02f1106cb03f558e2`, tag `Name=Girish-LLM`, `en.Owner=girish`, key pair name `QA-POC`, AMI `al2023-ami-2023.5.20240916.0` (Amazon Linux 2023), VPC `vpc-2ba7ca42`.
- Local `C:\Users\gkrishna\QA-POC.pem` does **not** match the AWS-registered fingerprint for key pair `QA-POC` (verified via `openssl pkcs8 ... | openssl md5` and SHA1-of-pubkey methods — neither matched `bf:c2:11:...`). SSH via that key fails with `Permission denied (publickey)`. Other candidate `.pem` files on disk (`girish-poc.pem`, `tln-load-test.pem`) also don't match.
- Workaround: instance has SSM Agent registered and `PingStatus: Online` (`aws ssm describe-instance-information`) — used `aws ssm send-command` with `AWS-RunShellScript` document to run diagnostic commands without needing the right SSH key.
- User prefers running the deploy script themselves on the instance (interactively, pasting output back) rather than me driving it fully via SSM — respect that; use SSM only for quick read-only diagnostics when explicitly needed, not for making changes, unless asked.
- Two real-world install errors hit and fixed in the script (in order): (1) `google-chrome` repo on this repurposed instance has a GPG key mismatch, which aborted `dnf update`/any `dnf install` that refreshed its metadata — fixed by disabling that repo outright before any installs; (2) Amazon Linux 2023's default repos don't actually carry `docker-compose-plugin` (contrary to the originally-approved infra design doc's assumption of `yum install docker-compose-plugin`) — fixed by falling back to downloading the official `docker-compose` v2 binary from GitHub releases into `/usr/local/lib/docker/cli-plugins`.
- Most recent blocker (not yet re-tested): user re-ran the script and got the *same old* dnf-compose error — diagnosed as stale code, not a real regression, because the log wording didn't match the latest script version. Then `sudo git pull origin main` from `/opt/lightning-mcp` failed with `fatal: not a git repository (or any of the parent directories): .git` — that directory isn't actually a git clone. The working clone is apparently at `~/aem-mcp` (seen in an earlier shell prompt `[ec2-user@ip-10-8-20-224 aem-mcp]$`) and needs the pull there instead.

## Files touched (this session, all committed & pushed to `origin/main`, repo: github.com/Girish-Krishna-AEM/aem.mcp)
- `unit-3/scripts/deploy-ec2.sh` — created, then patched 3x: drop blanket `dnf/yum update`, permanently disable broken `google-chrome` repo before installs, fall back to official Compose binary when the distro package is missing + verify `docker compose version` actually works before continuing.
- Earlier in this session (unrelated to current blocker, already shipped): real `get_daily_weather_forecast` / `get_hourly_weather_forecast` MCP tools backed by the internal staging forecast API (`unit-1/src/LightningMcpServer/ExternalApis/WeatherForecastApi*.cs`), Coordinator dynamic tool routing (`unit-2/.../QueryController.cs`), full request/response logging, `run-mcp-client-demo.ps1` MCP Inspector launch fix, `CLAUDE.md` + `.aidlc-rule-details/` now tracked in git, README overhaul.

## External Systems & Credentials
- AWS account `434906109792`, profile `qa` (SSO via `aws sso login --profile qa`, session was expired once this session, refreshed successfully).
- EC2 instance `i-02f1106cb03f558e2` / private IP `10.8.20.224` (no public IP — reachable from Girish's machine via VPN/corp network, confirmed via ping + port-22 TCP test).
- Internal staging weather forecast API: `http://stg.en.ods.forecasts.web.enstg.co` — its `locations` sub-dependency has been intermittently/persistently returning `503 Service Temporarily Unavailable` throughout this session (confirmed as an upstream outage, not our bug, via direct `curl` showing `{"Code":500,"ErrorMessage":"...ServiceUnavailable..."}`).
- GitHub repo: `github.com/Girish-Krishna-AEM/aem.mcp` (public). `.env` (with real `LIGHTNING_PULSE_API_KEY`) is gitignored, never committed.

## Decisions
- Use the official `docker-compose` v2 binary from GitHub releases as a fallback rather than adding Docker's official `download.docker.com` yum repo — avoids adding yet another third-party repo to an instance that already has one broken one.
- Keep `--disablerepo=google-chrome` on individual installs *and* permanently disable the repo file up front (belt-and-suspenders) — the per-command flag alone wasn't reliably sufficient.
- Script runs fully as root (`sudo ./deploy-ec2.sh`, checks `$EUID -ne 0`) rather than relying on docker-group membership propagating mid-script — simpler and avoids a re-login requirement.

## Assumptions (unverified)
- The real git clone on the instance is at `~/aem-mcp` under the `ec2-user` home directory — inferred from a shell prompt, not directly confirmed with `pwd`/`git remote -v`.
- `/opt/lightning-mcp` is empty/not a real clone — inferred from the `fatal: not a git repository` error; haven't confirmed whether it's fully empty or has partial stale content from an earlier aborted run.
- Instance has outbound internet access to `github.com` (user confirmed this earlier for the git-clone approach generally) and to `github.com/docker/compose/releases` for the new binary-fallback step specifically — not yet verified for this exact URL.

## Failing/blocked checks
- `sudo git pull origin main` from `/opt/lightning-mcp` — exact error: `fatal: not a git repository (or any of the parent directories): .git` — last attempted fix: none yet, next action is to `cd` to the correct directory instead (see Next actions #1–2).
- `dnf install -y docker-compose-plugin` on the instance — exact error: `No match for argument: docker-compose-plugin` / `Error: Unable to find a match: docker-compose-plugin` — fix already shipped (binary fallback in commit `18e4fa2`), not yet re-verified end-to-end on the instance.

## Rejected approaches
- SSH with local `.pem` files (`QA-POC.pem` and others found on disk) — none match the AWS-registered key pair fingerprint; would need the actual correct private key or an AWS key-pair reset to use SSH directly.
- Driving the full deployment myself via `aws ssm send-command` — user wants to run the script themselves interactively and paste output back; I used SSM only for read-only diagnostics (checking docker/git presence, repo config) when explicitly useful, not for making changes.

## Environment
- Local repo `C:\aem\ai-agentic\mcp`, branch `main`, clean working tree, fully in sync with `origin/main` at commit `18e4fa2`.
- AWS CLI profile `qa` authenticated (SSO), account `434906109792`, region `us-east-1`.

## Rollback point
- GitHub `main` @ `18e4fa2` ("Fall back to the official Compose binary when the distro package is unavailable") — last known-good, pushed state of `deploy-ec2.sh`.

## Open questions (blocked on user)
- Confirm the actual path of the real git clone on the instance (likely `~/aem-mcp`) and re-run `git pull && sudo ./unit-3/scripts/deploy-ec2.sh` from there, then paste output.

## Risks/watch-items
- The internal staging weather API's `locations` dependency has been down for most of this session (confirmed outside business hours per user) — once the stack is up, the daily/hourly weather tools will likely still fail with a `503`/upstream-error until that recovers; this is expected and already has clear error surfacing, not a deployment defect.
- This EC2 instance is a **repurposed** box with pre-existing unrelated state (the broken `google-chrome` repo, a running `timescaledb` container on port 5432, tag `QSConfigName-02fv2: QA Patching` suggesting scheduled OS patching via SSM Associations) — further unrelated surprises from prior uses of this instance are plausible; don't assume a clean POC environment.
- `/opt/lightning-mcp` existing-but-not-a-git-repo is unexplained — worth a quick `ls -la /opt/lightning-mcp` to see what's actually in there before deciding whether to delete it or leave it alone.
