# Manual release policy — September 30, 2026

Code publication and deployment are separate operations. A pull request merge or
push must not restart services, change public routing, invoke a production repair
endpoint, or automatically dispatch a deployment. Tests, static analysis,
artifacts, and container image publication remain permitted in CI.

This repair removes deployment jobs from the repository's combined CI workflow.
It does not execute or certify any remaining manually dispatched deployment
workflow. An image published by CI is a candidate, not a release acceptance signal.
Never select a mutable `latest` image without verifying its intended revision.

## Required handoff before production

1. Name the deployment owner and exact reviewed revision/image. Preserve unrelated
   operator changes and secret configuration. Coordinate with Cursor; a contested
   owner, freeze, or deployment lock stops the operation.
2. Follow the current Cursor procedure and the September 30 outage prevention
   record. Older generic restart or build-on-production recipes are superseded.
3. Website changes use blue-green: keep the known-good slot serving; build away
   from the production host; qualify the candidate directly before one cutover;
   retain the old healthy slot for rollback. Preserve the private environment,
   read-only NAS mount, exact network/slot alias, and qualified memory limits.
4. Show changed public UI to Morgan and obtain visual approval before deployment.
5. Record independent application, proxy, and public endpoint checks. A green build
   or an HTTP response from Cloudflare alone does not establish end-to-end health.
6. Record deployed revision, runtime evidence, and rollback destination separately
   from GitHub merge status. For backends, verify the actual service manager and
   dependent systems rather than reusing an unrelated website recipe.

Canonical workspace references: `CODE/docs/WEBSITE_OUTAGE_NEVER_AGAIN_SEP30_2026.md`,
`CODE/docs/BLUE_GREEN_NEVER_502_SOLE_OWNER_SEP23_2026.md`, and Cursor's
`MAS/mycosoft-mas/.cursor/agents/deploy-pipeline.md`. These references define a
procedure; they are not authorization to run stale example commands.

## Scope and rollback

This is a CI safety change; no service, database, device, DNS record, or live
container is modified. Restore removed job bodies from Git history only through
a separately reviewed change that still prevents automatic deployment. Reverting
this policy wholesale would re-enable known automatic operational paths.
