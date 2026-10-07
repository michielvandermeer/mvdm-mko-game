# Changing the schema

1. Add an `IDbMigration` class to `Underclass.Data`, named `_<yyyyMMddHHmm>_<Name>`, with its SQL in `schema.sql`'s
   style. The API runs pending migrations at startup.
2. Read what the migration adds in `TownRepository.LoadAsync`, and write it in `SaveAsync`. A new table holding part of
   a Town also needs:
   - `town_id uuid NOT NULL REFERENCES underclass.towns (town_id) ON DELETE CASCADE`;
   - a place in `TownRepository.DeletionOrder`, children before parents, and in `SaveAsync`'s inserts, parents first;
   - a place in `TruncateTown` in `tests/Underclass.Api.Tests/UnderclassApi.cs`.
3. When folding migrations into `schema.sql`, raise its `-- Migration version:` header to the last folded migration,
   or a fresh database runs that migration again.
