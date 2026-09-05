# LectureVault

ASP.NET Core MVC (.NET 10) web application, run via Docker.

## Krav

- [Docker](https://docs.docker.com/get-docker/) og Docker Compose (Docker Desktop inkluderer begge).

## Kom i gang

1. Klon repoet:

   ```bash
   git clone <repo-url>
   cd LectureVault
   ```

2. Kopiér miljøvariabel-skabelonen og udfyld de værdier du har brug for:

   ```bash
   cp .env.example .env
   ```

   Alle felter i `.env.example` er valgfrie og kan stå tomme — appen starter fint uden dem, men
   funktioner der kræver en integration (AI-chat, transskription, SMS, e-mail) er deaktiveret
   indtil de tilhørende variabler er udfyldt. Se kommentarerne i `.env.example` for hvad hvert
   felt styrer.

3. Byg og start containeren:

   ```bash
   docker compose up -d --build
   ```

4. Åbn appen på [http://localhost:8080](http://localhost:8080).

   Ved første besøg redirectes du automatisk til `/Setup/FirstUser`, hvor den første bruger
   oprettes og får rollen `Developer`.

## Data og persistens

SQLite-databaserne (`app.db`, `logs.db`) og uploadede filer gemmes i to Docker-volumes, så data
overlever genstart og genbygning af containeren:

- `lecturevault_dbs` → `/app/App_dbs`
- `lecturevault_files` → `/app/App_files`

Migrationer og seed-data (roller, standardindstillinger, temaer) kører automatisk ved opstart —
der kræves ingen manuel `dotnet ef database update`.

## Nyttige kommandoer

```bash
docker compose up -d --build   # (gen)byg og start i baggrunden
docker compose logs -f web     # følg logs
docker compose down            # stop containeren (data bevares i volumes)
docker compose down -v         # stop OG slet volumes (nulstiller al data — brug med omtanke)
docker compose restart web     # genstart containeren
```

## Konfiguration

Alle indstillinger fra `web/appsettings.json` kan overstyres via miljøvariabler i `.env`, ved at
erstatte `:` i konfigurationsstien med `__` (dobbelt underscore), fx `AiGateway:BaseUrl` bliver
`AiGateway__BaseUrl`. `.env.example` indeholder de mest relevante nøgler i forvejen.

`.env` er git-ignoreret og må aldrig committes, da den kan indeholde rigtige API-nøgler og
adgangskoder.

## Bemærkninger

- Containeren serverer kun almindelig HTTP på port 8080 (ingen TLS). Appens
  `UseHttpsRedirection()` kan derfor logge en advarsel om at den ikke kan bestemme en
  HTTPS-port — det er forventet og harmløst. Skal appen eksponeres direkte på internettet,
  sæt en reverse proxy (nginx, Caddy, Traefik e.l.) foran til TLS-terminering.

## Udvikling uden Docker

Se [CLAUDE.md](CLAUDE.md) for kommandoer til at bygge/køre projektet direkte med `dotnet` samt en
oversigt over arkitekturen.
