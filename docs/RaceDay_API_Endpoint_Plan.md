# RaceDay — API Endpoint Plan (Section B)

Roles: **Public** (not logged in) · **Any** (any logged-in user) · **Organiser** · **Participant**

Authentication uses a **session cookie** (`RaceDay.Session`) set by the login endpoint, as required by the Part 2 brief. The cookie carries the user's id and role.

| # | HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|---|
| 1 | POST | `/api/auth/register` | Registers a new user as an Organiser or a Participant. Organiser registration needs an invite code so nobody can make themselves an organiser. | None (public) | `{ fullName, email, password, role, organiserCode }` (organiserCode only when role = Organiser) | 201 Created - new user (no password). 400 Bad Request - invalid data/role. 403 Forbidden - bad organiser code. 409 Conflict - email already in use. |
| 2 | POST | `/api/auth/login` | Authenticates a user and starts a session (cookie). | None (public) | `{ email, password }` | 200 OK - `{ userID, fullName, role, message }` + session cookie. 401 Unauthorized - invalid credentials. |
| 3 | POST | `/api/auth/logout` | Ends the current session. | Any (logged in) | None | 200 OK - confirmation. 401 Unauthorized. |
| 4 | GET | `/api/users/me` | Returns the profile of the logged-in user. | Any (logged in) | None | 200 OK - user profile. 401 Unauthorized. |
| 5 | PUT | `/api/users/me` | Updates the logged-in user's own profile details. | Any (logged in) | `{ fullName, email }` | 200 OK - updated profile. 400 Bad Request. 409 Conflict - email in use. |
| 6 | GET | `/api/events` | Lists events (soonest first), optionally filtered by `type` or `location`. | None (public) | None | 200 OK - array of events. |
| 7 | GET | `/api/events/{id}` | Returns one event with its categories and routes. | None (public) | None | 200 OK - event detail. 404 Not Found. |
| 8 | POST | `/api/events` | Creates a new event owned by the logged-in organiser. | Organiser | `{ eventName, eventDate, location, eventType, distanceKm, description }` | 201 Created - new event. 400 Bad Request. 403 Forbidden - not an organiser. |
| 9 | PUT | `/api/events/{id}` | Edits an event owned by the logged-in organiser. | Organiser | `{ eventName, eventDate, location, eventType, distanceKm, description }` | 200 OK - updated event. 403 Forbidden - not the owner. 404 Not Found. |
| 10 | DELETE | `/api/events/{id}` | Deletes an event (and its categories/routes) owned by the logged-in organiser. | Organiser | None | 200 OK - confirmation. 403 Forbidden. 404 Not Found. 409 Conflict - event has enrolments. |
| 11 | GET | `/api/events/{id}/categories` | Lists all categories for a specific event. | None (public) | None | 200 OK - array of categories. 404 Not Found. |
| 12 | POST | `/api/events/{id}/categories` | Adds a category (distance or age group, e.g. 10km, Under 20, Senior) to an event. | Organiser | `{ categoryName, distanceKm (optional), entryFee, maxParticipants }` | 201 Created - new category. 403 Forbidden - not the owner. 404 Not Found. |
| 13 | PUT | `/api/categories/{id}` | Edits an existing category. | Organiser | `{ categoryName, distanceKm (optional), entryFee, maxParticipants }` | 200 OK - updated category. 403 Forbidden. 404 Not Found. |
| 14 | DELETE | `/api/categories/{id}` | Removes a category from an event. | Organiser | None | 200 OK - confirmation. 403 Forbidden. 404 Not Found. 409 Conflict - category has enrolments. |
| 15 | POST | `/api/categories/{id}/enrol` | Enrols the logged-in participant into a category (and so the event). | Participant | None (participant identified from the session) | 201 Created - enrolment record. 403 Forbidden - not a participant. 404 Not Found - category does not exist. 409 Conflict - already enrolled in the event, or category full. |
| 16 | GET | `/api/users/me/enrolments` | Lists the logged-in participant's own enrolments. | Participant | None | 200 OK - array of enrolments. 403 Forbidden. |
| 17 | DELETE | `/api/enrolments/{id}` | Cancels the logged-in participant's own enrolment (status becomes Cancelled). | Participant | None | 200 OK - confirmation. 403 Forbidden - not the owner. 404 Not Found. 409 Conflict - already cancelled or result captured. |
| 18 | GET | `/api/events/{id}/enrolments` | Lists all enrolments for an event (roster view). | Organiser | None | 200 OK - array of enrolments with participant details. 403 Forbidden - not the owner. 404 Not Found. |
| 19 | POST | `/api/enrolments/{id}/result` | Captures a race result for a specific enrolment. | Organiser | `{ finishTime, overallPosition, categoryPosition, status }` | 201 Created - result record. 400 Bad Request. 403 Forbidden. 404 Not Found. 409 Conflict - result already captured. |
| 20 | PUT | `/api/results/{id}` | Edits a previously captured result. | Organiser | `{ finishTime, overallPosition, categoryPosition, status }` | 200 OK - updated result. 403 Forbidden. 404 Not Found. |
| 21 | GET | `/api/users/me/results` | Returns the logged-in participant's own results history. | Participant | None | 200 OK - array of results. 403 Forbidden. |
| 22 | GET | `/api/events/{id}/results` | Returns the full results list for an event (leaderboard). | None (public) | None | 200 OK - array of results ranked by position. 404 Not Found. |
| 23 | GET | `/api/events/mine` | Lists the logged-in organiser's own events with category count and confirmed enrolment total (dashboard data). | Organiser | None | 200 OK - array of event summaries. 403 Forbidden - not an organiser. |
| 24 | POST | `/api/events/{id}/banner` | Uploads or replaces the event banner image (multipart/form-data, field `file`; JPG/PNG/GIF/WebP up to 5 MB). Stored in Azure Blob Storage. | Organiser | `file` (multipart) | 200 OK - updated event (`hasBanner` true). 400 Bad Request - not an image / too large. 403 Forbidden - not the owner. 404 Not Found. |
| 25 | GET | `/api/events/{id}/banner` | Streams the event banner image from Azure Blob Storage. | None (public) | None | 200 OK - image file. 404 Not Found - no banner. |
| 26 | POST | `/api/users/me/profile-picture` | Uploads or replaces the logged-in user's profile picture (multipart/form-data, field `file`). Stored in Azure Blob Storage. | Any (logged in) | `file` (multipart) | 200 OK - updated profile (`hasProfilePicture` true). 400 Bad Request. 401 Unauthorized. |
| 27 | GET | `/api/users/me/profile-picture` | Streams the logged-in user's profile picture. | Any (logged in) | None | 200 OK - image file. 401 Unauthorized. 404 Not Found - no picture. |

**Coverage check against the ERD/schema:**
- Authentication → rows 1–3
- User Profile → rows 4–5
- Events → rows 6–10
- Categories → rows 11–14
- Event Enrolments → rows 15–18
- Results → rows 19–22
- Images (Part 3, Azure Blob Storage) → rows 24–27; organiser dashboard → row 23

**Changes made while preparing Part 2 (explained):**
- Login uses a session cookie instead of returning a token, because the Part 2 brief requires session management.
- `POST /api/auth/logout` added so a session can be ended.
- `organiserCode` added to registration so the Organiser role cannot be self-assigned by anyone.
- `distanceKm` added to events (brief: each event captures a distance); category distance is optional so age categories ("Under 20", "Senior") work.
- 409 responses added for delete-with-enrolments, duplicate enrolment, and full categories.

**Changes made while preparing Part 3 (explained):**
- Added rows 23–27 for the organiser dashboard, event banner images and profile pictures (stored in Azure Blob Storage; the MVC app never calls Azure directly).
- Event, user and result responses gained `hasBanner`, `hasProfilePicture`, `eventDate` and `totalFinishers` so the MVC pages can show images and "47th out of 312 finishers".
