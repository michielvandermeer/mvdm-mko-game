# Running the stack by hand

Start Postgres and the API as [`client/godot/README.md`](../../client/godot/README.md) describes under _Running it_.
Then:

- Connect to the stack's Postgres on host port 5433, or through the container:
  `docker exec -it underclass-postgres psql -U underclass -d underclass`. Port 5432 usually belongs to another Postgres,
  holding none of this game's data. The game's tables are in schema `underclass`; job storage and the migration
  history (`mvdmio.jobs`, `mvdmio.migrations`) are in schema `mvdmio`.
- Drive the API without the client through curl. Bodies are camelCase JSON, and enum values travel as names:
  `curl -s -X POST localhost:5268/api/crafting/craft -H 'Content-Type: application/json' -d '{"baseType":"Sword","material":"Copper"}'`.
  The routes are in `src/Underclass.Api/Endpoints/UnderclassEndpoints.cs`.
- Start from a fresh Town with `docker compose down -v`, which drops the volume; after `docker compose up -d` and an
  API restart, the next `GET /api/town` creates the starting Town.
- Reproduce a run's rolls from a fresh Town by setting `RandomSeed`:
  `RandomSeed=42 dotnet run --project src/Underclass.Api --urls http://localhost:5268`.
- Expect real waits: the running API reads the system clock, so an Expedition takes its full duration, in minutes per
  depth (`Catalogue.Areas`). To see a resolution without waiting, write the case as an API test, whose clock the test
  moves.
