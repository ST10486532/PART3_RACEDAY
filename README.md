# RaceDay

RaceDay is a web platform for South African road running, walking and cycling events.
**Organisers** create events, categories and results; **Participants** browse events, enrol in a category and track their race history.

| Part | What | Where |
|---|---|---|
| 1 | ERD, API endpoint plan, SQL script | `/docs` |
| 2 | ASP.NET Core Web API, EF Core (Code-First), Swagger, unit tests, CI | `/src/RaceDay.Api`, `/tests/RaceDay.Tests` |
| 3 | ASP.NET Core MVC front end, Azure Blob Storage, Docker | `/src/RaceDay.Web`, `Dockerfile`, `docker-compose.yml` |

**Demo video (YouTube, unlisted):** _paste the link here_

## Architecture

```
 Browser  ->  RaceDay.Web (MVC)  ->  RaceDay.Api (REST API)  ->  SQL Server
                                            |
                                            +-->  Azure Blob Storage (event banners, profile pictures)
```

* **RaceDay.Web (MVC)** has no database access and no business rules. Every piece of data comes from the API through one typed client (`Services/RaceDayApi.cs`).
* **RaceDay.Api** owns the data, the rules, authentication and file storage.
* **The MVC app never talks to Azure.** Images are uploaded to the API (`POST /api/events/{id}/banner`, `POST /api/users/me/profile-picture`); the API stores them in a private blob container and streams them back when asked. The MVC app shows them through `/media/events/{id}` and `/media/profile`.

### MVC structure
| Folder | Purpose |
|---|---|
| `Controllers/` | `Home` (events list), `Events` (event details), `Account` (register, login, profile), `Organiser` (dashboard, events, categories, enrolments, results), `Participant` (enrol, my enrolments, my results), `Media` (image proxy) |
| `Services/` | `IRaceDayApi` / `RaceDayApi` - the only code that talks to the API; `ApiResult`, exceptions, session keys |
| `Filters/` | `ApiExceptionFilter` - expired session -> login, wrong role -> access denied, API down -> friendly page |
| `Models/` | Copies of the API's JSON shapes (DTOs) |
| `ViewModels/` | What each page needs, with validation messages |
| `Views/` | Razor pages, one folder per controller, shared layout/partials |
| `wwwroot/` | Theme (`css/site.css`) and small script |

### Sessions, authentication and roles
1. The user logs in on the MVC site; MVC calls `POST /api/auth/login`.
2. The API replies with its session cookie. MVC keeps that cookie **in the user's server-side session** (never in the browser's reach) and sends it with every later API call, so the API always knows who is calling and what role they have.
3. MVC also keeps the user's name and role in the session and signs them in with a cookie that carries a role claim, so `[Authorize(Roles = "Organiser")]` and `[Authorize(Roles = "Participant")]` protect the pages.
4. Unauthenticated users are redirected to the login page; a participant opening an organiser page (or the reverse) sees "access denied".
5. Log out (nav bar) ends both sessions and returns to the login page. If the API session expires, the user is sent back to log in with a message.

### Pages
**Organiser:** dashboard (events created, total enrolments, upcoming dates), event management (create / edit / delete with name, description, date, location, distance, type and a banner image), categories (age or distance), enrolments per event (with status), results entry (finish time + finishing positions, published as soon as they are saved).
**Participant:** home page with filters, event details with categories, one-click enrolment, my enrolments (with colour-coded status), my results (e.g. "47th out of 312 finishers"), profile with picture upload.
**Status colours:** green = Confirmed, amber = Pending / Awaiting result, red = Cancelled, blue = Upcoming.

## Run it with Docker (one command)

Requires Docker Desktop.

```bash
docker compose up --build
```

| What | URL |
|---|---|
| MVC web app | http://localhost:8080 |
| API + Swagger | http://localhost:5080/swagger |

This starts four containers: **SQL Server**, **Azurite** (Microsoft's local Azure Blob Storage emulator), the **API** (it creates and migrates the database on start-up) and the **MVC app**. Stop with `Ctrl+C`; `docker compose down -v` also deletes the data.

### Run only the MVC container
If the API is already running on your computer (for example from Visual Studio):

```bash
docker build -t raceday-web .
docker run --rm -p 8080:8080 -e Api__BaseUrl=http://host.docker.internal:5080 raceday-web
```

Then open http://localhost:8080. For a container to reach an API started on your computer, the API must listen on all interfaces, e.g. `dotnet run --project src/RaceDay.Api --urls http://0.0.0.0:5080`. (On Linux add `--add-host=host.docker.internal:host-gateway` to `docker run`.)

### Using a real Azure Storage account instead of Azurite
Create a Storage Account in the Azure portal, copy its **connection string**, and set it on the API (either in `docker-compose.yml` under the `api` service, or as an environment variable):

```
AzureBlob__ConnectionString=<your connection string>
AzureBlob__ContainerName=raceday-images
```
The code is identical; only the connection string changes. The container is created automatically and stays **private**.

## Run it locally (without Docker)
1. **API** - open `RaceDay.sln` in Visual Studio, check the connection string in `src/RaceDay.Api/appsettings.json`, and run `RaceDay.Api` (http://localhost:5080/swagger). A fresh database is created from the EF Core model on start-up. When using an existing Part 2 database, the API adds the two nullable image-blob columns if they are missing.
   For image uploads either start Azurite (`npx azurite-blob` or the Azurite extension in Visual Studio) or set `AzureBlob:ConnectionString` to a real storage account.
2. **MVC** - run `RaceDay.Web` (http://localhost:5100). It calls the API at `Api:BaseUrl` (default `http://localhost:5080`).

## Try it out (demo script)
1. Register an **Organiser** (invite code `RACEDAY-ORG-2026`) and a **Participant** (use a private window for the second one).
2. As the organiser: create an event (pick a date in the past to capture results straight away, or a future date to see it on the home page), upload a banner, add categories (e.g. `10km` and `Under 20`).
3. As the participant: open the event, enrol in a category, check *My enrolments*, upload a profile picture.
4. As the organiser: open the event's *Enrolments*, then *Results* and publish a finish time and position.
5. As the participant: open *My results*.

## Tests
```bash
dotnet test
```
66 tests run the real API in memory (in-memory database and a fake image store, so no Azure needed). They cover registration/login, event management, role rejection (401/403), enrolments, results, banner and profile-picture uploads (valid, wrong type, too large, wrong role) and the dashboard data.

## Continuous integration
`.github/workflows/ci.yml` builds the API **and the MVC project**, runs all unit tests, then builds both Docker images on pushes to the configured branches and on pull requests to `main`. Add screenshots of successful workflow runs here after pushing the completed project to GitHub.

## API changes made for Part 3
| Change | Why |
|---|---|
| `GET /api/events/mine` | Organiser dashboard: own events with enrolment totals |
| `POST /api/events/{id}/banner`, `GET /api/events/{id}/banner` | Event banner stored in Azure Blob Storage |
| `POST /api/users/me/profile-picture`, `GET /api/users/me/profile-picture` | Profile picture stored in Azure Blob Storage |
| `totalFinishers` and `eventDate` on results | "47th out of 312 finishers" on *My results* |
| `hasBanner` / `hasProfilePicture` flags | The UI only requests images that exist |
| Startup schema update | Creates a fresh database from the EF Core model and adds the two nullable image columns to an existing Part 2 database |
| Database start-up retry (containers only) | SQL Server may need a few seconds to accept connections |

The endpoint plan in `docs/RaceDay_API_Endpoint_Plan.md` has been updated.
