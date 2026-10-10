# Migration review: StandardizePhysicalIdentifiers

Migration: `20260827081000_StandardizePhysicalIdentifiers`
Artifact: `manufacturing-idempotent.sql`

This migration is an intentional physical-schema compatibility change. It normalizes legacy mixed-case PostgreSQL column identifiers to snake_case. When both names exist, it refuses to merge populated columns, copies only into null destinations, and then removes the legacy column. It is therefore destructive by design and must not be treated as a routine additive migration.

## Expand/contract controls

- Run the migration only through the one-shot migration Job after a backup or restore-copy rehearsal.
- Preflight `information_schema.columns` and verify that every collision is either empty on the legacy side or explicitly remediated before execution.
- Keep API replicas at `Persistence:RunMigrationsOnStartup=false` and deploy the compatible application version before the schema change.
- Validate representative tenant tables, indexes, foreign keys, and outbox writes after the job; abort on any collision exception.

## Rollback and recovery

The EF `Down()` method intentionally does not rename identifiers automatically because data-preserving reverse renames require an environment-specific inventory. Recovery is through the pre-migration backup/restore procedure, followed by a forward-compatible corrective migration. Production approval must confirm the backup and restore rehearsal before applying this job.
