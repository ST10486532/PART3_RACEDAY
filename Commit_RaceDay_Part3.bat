@echo off
rem Makes the Part 3 commits, one logical piece at a time. Run from the repository folder (e.g. C:\Users\Student\RaceDay).
git rev-parse --verify part3-mvc >nul 2>&1
if errorlevel 1 (
  git checkout -b part3-mvc
) else (
  git checkout part3-mvc
)

git add src\RaceDay.Api\RaceDay.Api.csproj src\RaceDay.Api\appsettings.json
git commit -m "Add Azure.Storage.Blobs package and blob storage settings to the API"

git add src\RaceDay.Api\Services\IImageStorage.cs
git commit -m "Add IImageStorage abstraction so controllers never depend on Azure directly"

git add src\RaceDay.Api\Services\AzureBlobImageStorage.cs
git commit -m "Add Azure Blob Storage implementation of image storage"

git add src\RaceDay.Api\Services\ImageValidator.cs
git commit -m "Add image validation by size and file signature"

git add src\RaceDay.Api\Models\RaceEvent.cs src\RaceDay.Api\Models\AppUser.cs src\RaceDay.Api\Data\RaceDayContext.cs
git commit -m "Add banner and profile picture blob columns to the model"

git add src\RaceDay.Api\Migrations\20261108120000_AddImageColumns.cs
git commit -m "Add AddImageColumns migration"

git add src\RaceDay.Api\Data\ResultQueries.cs src\RaceDay.Api\Dtos\ResultDtos.cs
git commit -m "Add finisher totals and event date to results"

git add src\RaceDay.Api\Dtos\EventDtos.cs src\RaceDay.Api\Dtos\AuthDtos.cs
git commit -m "Expose banner and profile picture flags and the organiser summary DTO"

git add src\RaceDay.Api\Controllers\EventsController.cs
git commit -m "Add organiser summary and event banner upload and download endpoints"

git add src\RaceDay.Api\Controllers\UsersController.cs
git commit -m "Add profile picture endpoints and finisher totals on my results"

git add src\RaceDay.Api\Controllers\EnrolmentsController.cs src\RaceDay.Api\Controllers\ResultsController.cs
git commit -m "Return finisher totals when capturing or editing results"

git add src\RaceDay.Api\Program.cs
git commit -m "Register blob storage and add database start-up retry for containers"

git add tests\RaceDay.Tests\InMemoryImageStorage.cs tests\RaceDay.Tests\RaceDayFactory.cs
git commit -m "Add in-memory image storage so tests never call Azure"

git add tests\RaceDay.Tests\ImageTests.cs
git commit -m "Add banner and profile picture upload tests"

git add tests\RaceDay.Tests\DashboardTests.cs
git commit -m "Add organiser summary and finisher total tests"

git add src\RaceDay.Web\RaceDay.Web.csproj src\RaceDay.Web\appsettings.json src\RaceDay.Web\Properties
git commit -m "Create ASP.NET Core MVC project"

git add src\RaceDay.Web\Models\ApiModels.cs
git commit -m "Add API data models for the MVC client"

git add src\RaceDay.Web\Services\SessionKeys.cs src\RaceDay.Web\Services\ApiExceptions.cs src\RaceDay.Web\Services\ApiResult.cs
git commit -m "Add session keys, API exceptions and result type"

git add src\RaceDay.Web\Services\IRaceDayApi.cs src\RaceDay.Web\Services\RaceDayApi.cs
git commit -m "Add typed API client that forwards the API session cookie"

git add src\RaceDay.Web\Filters\ApiExceptionFilter.cs
git commit -m "Add filter for expired sessions, access denied and API outages"

git add src\RaceDay.Web\Program.cs
git commit -m "Configure session, cookie auth, antiforgery and the API HttpClient"

git add src\RaceDay.Web\ViewModels\AccountViewModels.cs src\RaceDay.Web\ViewModels\EventViewModels.cs src\RaceDay.Web\ViewModels\ParticipantViewModels.cs
git commit -m "Add view models with friendly validation messages"

git add src\RaceDay.Web\Helpers\UiHelpers.cs
git commit -m "Add UI helpers for status colours, ordinals and formatting"

git add src\RaceDay.Web\Controllers\WebControllerBase.cs src\RaceDay.Web\Controllers\AccountController.cs
git commit -m "Add account controller: register, login, logout and profile"

git add src\RaceDay.Web\Controllers\HomeController.cs src\RaceDay.Web\Controllers\EventsController.cs src\RaceDay.Web\Controllers\MediaController.cs
git commit -m "Add home, event details and image proxy controllers"

git add src\RaceDay.Web\Controllers\OrganiserController.cs
git commit -m "Add organiser controller: dashboard, events, categories, enrolments and results"

git add src\RaceDay.Web\Controllers\ParticipantController.cs
git commit -m "Add participant controller: enrol, my enrolments and my results"

git add src\RaceDay.Web\wwwroot
git commit -m "Add RaceDay sporting theme and script"

git add src\RaceDay.Web\Views\_ViewImports.cshtml src\RaceDay.Web\Views\_ViewStart.cshtml src\RaceDay.Web\Views\Shared
git commit -m "Add layout with role-based navigation and shared partials"

git add src\RaceDay.Web\Views\Account
git commit -m "Add account views: login, register, profile and access denied"

git add src\RaceDay.Web\Views\Home src\RaceDay.Web\Views\Events
git commit -m "Add home page with filters and event details views"

git add src\RaceDay.Web\Views\Organiser\Dashboard.cshtml src\RaceDay.Web\Views\Organiser\Events.cshtml src\RaceDay.Web\Views\Organiser\EventForm.cshtml
git commit -m "Add organiser dashboard and event management views"

git add src\RaceDay.Web\Views\Organiser\Categories.cshtml src\RaceDay.Web\Views\Organiser\EditCategory.cshtml src\RaceDay.Web\Views\Organiser\_CategoryForm.cshtml src\RaceDay.Web\Views\Organiser\_EventTabs.cshtml
git commit -m "Add organiser category management views"

git add src\RaceDay.Web\Views\Organiser\Enrolments.cshtml src\RaceDay.Web\Views\Organiser\Results.cshtml
git commit -m "Add organiser enrolments and results entry views"

git add src\RaceDay.Web\Views\Participant
git commit -m "Add participant views: my enrolments and my results"

git add Dockerfile .dockerignore
git commit -m "Add Dockerfile for the MVC application"

git add Dockerfile.api docker-compose.yml
git commit -m "Add API Dockerfile and docker-compose for the full stack"

git add RaceDay.sln
git commit -m "Add the MVC project to the solution"

git add .github\workflows\ci.yml
git commit -m "Update CI workflow to build the MVC project and Docker images"

git add docs\RaceDay_API_Endpoint_Plan.md
git commit -m "Update API endpoint plan with Part 3 endpoints"

git add README.md
git commit -m "Rewrite README with architecture, Docker instructions and demo steps"

echo.
echo ===== STATUS =====
git status
echo ===== LOG =====
git log --oneline -n 60
echo.
echo If the status says "working tree clean", run:  git push -u origin part3-mvc
