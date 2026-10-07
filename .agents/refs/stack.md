# Running the stack by hand

Start Postgres and the API as [`client/godot/README.md`](../../client/godot/README.md) describes under _Running it_.
Then:

- Connect to the stack's Postgres on host port 5433 (5432 is usually another local Postgres), or through the
  container: `docker exec -it underclass-postgres psql -U underclass -d underclass`.
- Drive the API without the client through curl, with camelCase JSON bodies and enum values as names:
  `curl -s -X POST localhost:5268/api/crafting/craft -H 'Content-Type: application/json' -d '{"baseType":"Sword","material":"Copper"}'`.
- Start from a fresh Town with `docker compose down -v`, which drops the volume; after `docker compose up -d` and an
  API restart, the next `GET /api/town` creates the starting Town.
- Reproduce a run's rolls from a fresh Town by setting `RandomSeed`:
  `RandomSeed=42 dotnet run --project src/Underclass.Api --urls http://localhost:5268`.
- Expect real waits: the running API reads the system clock, so an Expedition takes its full duration. To see a
  resolution without waiting, write the case as an API test, whose clock the test moves.
