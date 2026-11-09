/* ============================================================
   RaceDay - Full Database Schema (SQL Server / SSMS)
   Matches /docs/RaceDay_ERD.png exactly (6 entities).
   ============================================================ */

IF DB_ID('RaceDayDB') IS NULL
BEGIN
    CREATE DATABASE RaceDayDB;
END
GO

USE RaceDayDB;
GO

/* ------------------------------------------------------------
   Drop tables if they already exist (child -> parent order)
   ------------------------------------------------------------ */
IF OBJECT_ID('dbo.Results', 'U')      IS NOT NULL DROP TABLE dbo.Results;
IF OBJECT_ID('dbo.Enrolments', 'U')   IS NOT NULL DROP TABLE dbo.Enrolments;
IF OBJECT_ID('dbo.Routes', 'U')       IS NOT NULL DROP TABLE dbo.Routes;
IF OBJECT_ID('dbo.Categories', 'U')   IS NOT NULL DROP TABLE dbo.Categories;
IF OBJECT_ID('dbo.Events', 'U')       IS NOT NULL DROP TABLE dbo.Events;
IF OBJECT_ID('dbo.Users', 'U')        IS NOT NULL DROP TABLE dbo.Users;
GO

/* ============================================================
   1. Users  (Organisers and Participants share this table,
              differentiated by Role)
   ============================================================ */
CREATE TABLE dbo.Users (
    UserID          INT IDENTITY(1,1) PRIMARY KEY,
    FullName        VARCHAR(100)    NOT NULL,
    Email           VARCHAR(150)    NOT NULL UNIQUE,
    PasswordHash    VARCHAR(255)    NOT NULL,
    Role            VARCHAR(20)     NOT NULL
                        CONSTRAINT CK_Users_Role CHECK (Role IN ('Organiser','Participant')),
    CreatedAt       DATETIME        NOT NULL DEFAULT GETDATE()
);
GO

/* ============================================================
   2. Events  (created and owned by an Organiser)
   ============================================================ */
CREATE TABLE dbo.Events (
    EventID         INT IDENTITY(1,1) PRIMARY KEY,
    OrganiserID     INT             NOT NULL,
    EventName       VARCHAR(150)    NOT NULL,
    EventDate       DATE            NOT NULL,
    Location        VARCHAR(150)    NOT NULL,
    EventType       VARCHAR(20)     NOT NULL
                        CONSTRAINT CK_Events_Type CHECK (EventType IN ('Run','Walk','Cycle')),
    DistanceKm      DECIMAL(5,2)    NOT NULL,
    Description     VARCHAR(1000)   NULL,
    CONSTRAINT FK_Events_Organiser FOREIGN KEY (OrganiserID)
        REFERENCES dbo.Users(UserID)
);
GO

/* ============================================================
   3. Categories  (distance/class options within an Event)
   ============================================================ */
CREATE TABLE dbo.Categories (
    CategoryID      INT IDENTITY(1,1) PRIMARY KEY,
    EventID         INT             NOT NULL,
    CategoryName    VARCHAR(100)    NOT NULL,
    DistanceKm      DECIMAL(5,2)    NULL,   -- NULL for age-based categories (e.g. 'Under 20', 'Senior')
    EntryFee        DECIMAL(8,2)    NOT NULL DEFAULT 0,
    MaxParticipants INT             NOT NULL DEFAULT 100,
    CONSTRAINT FK_Categories_Event FOREIGN KEY (EventID)
        REFERENCES dbo.Events(EventID)
);
GO

/* ============================================================
   4. Routes  (route/map info per Event)
   ============================================================ */
CREATE TABLE dbo.Routes (
    RouteID         INT IDENTITY(1,1) PRIMARY KEY,
    EventID         INT             NOT NULL,
    RouteName       VARCHAR(100)    NOT NULL,
    DistanceKm      DECIMAL(5,2)    NOT NULL,
    ElevationGainM  DECIMAL(6,2)    NULL,
    MapUrl          VARCHAR(255)    NULL,
    CONSTRAINT FK_Routes_Event FOREIGN KEY (EventID)
        REFERENCES dbo.Events(EventID)
);
GO

/* ============================================================
   5. Enrolments  (a Participant entering a Category)
   ============================================================ */
CREATE TABLE dbo.Enrolments (
    EnrolmentID     INT IDENTITY(1,1) PRIMARY KEY,
    ParticipantID   INT             NOT NULL,
    CategoryID      INT             NOT NULL,
    EnrolmentDate   DATETIME        NOT NULL DEFAULT GETDATE(),
    Status          VARCHAR(20)     NOT NULL DEFAULT 'Confirmed'
                        CONSTRAINT CK_Enrolments_Status CHECK (Status IN ('Confirmed','Cancelled')),
    CONSTRAINT FK_Enrolments_Participant FOREIGN KEY (ParticipantID)
        REFERENCES dbo.Users(UserID),
    CONSTRAINT FK_Enrolments_Category FOREIGN KEY (CategoryID)
        REFERENCES dbo.Categories(CategoryID),
    CONSTRAINT UQ_Enrolments_Participant_Category UNIQUE (ParticipantID, CategoryID)
);
GO

/* ============================================================
   6. Results  (one result per Enrolment, captured after race day)
   ============================================================ */
CREATE TABLE dbo.Results (
    ResultID            INT IDENTITY(1,1) PRIMARY KEY,
    EnrolmentID         INT             NOT NULL UNIQUE,
    FinishTime          TIME            NULL,
    OverallPosition      INT             NULL,
    CategoryPosition    INT             NULL,
    Status              VARCHAR(20)     NOT NULL DEFAULT 'Finished'
                            CONSTRAINT CK_Results_Status CHECK (Status IN ('Finished','DNF','DQ')),
    CONSTRAINT FK_Results_Enrolment FOREIGN KEY (EnrolmentID)
        REFERENCES dbo.Enrolments(EnrolmentID)
);
GO

/* ============================================================
   SEED DATA
   ============================================================ */

-- 2 Organisers + 2 Participants (4 Users minimum -> exceeds the 2+2 requirement)
INSERT INTO dbo.Users (FullName, Email, PasswordHash, Role) VALUES
('Thabo Mokoena',   'thabo.mokoena@raceday.co.za',  'hash_org_001', 'Organiser'),
('Sarah van Wyk',    'sarah.vanwyk@raceday.co.za',   'hash_org_002', 'Organiser'),
('Lindiwe Dube',     'lindiwe.dube@gmail.com',       'hash_par_001', 'Participant'),
('James Botha',      'james.botha@gmail.com',        'hash_par_002', 'Participant');
GO

-- 3 Events, one per organiser (Thabo runs two, Sarah runs one)
INSERT INTO dbo.Events (OrganiserID, EventName, EventDate, Location, EventType, DistanceKm, Description) VALUES
(1, 'Johannesburg City Run',    '2026-10-04', 'Sandton, Johannesburg', 'Run',   10.00, 'Annual road run through the Sandton CBD.'),
(1, 'Soweto Heritage Walk',     '2026-10-18', 'Soweto, Johannesburg',  'Walk',   3.00, 'Community charity walk along heritage sites.'),
(2, 'Cape Winelands Cycle Tour','2026-11-08', 'Stellenbosch, Cape Town','Cycle',80.00, 'Scenic cycling tour through the Winelands.');
GO

-- Categories for each of the 3 events
INSERT INTO dbo.Categories (EventID, CategoryName, DistanceKm, EntryFee, MaxParticipants) VALUES
(1, '5km Fun Run',      5.00,  100.00, 500),
(1, '10km Challenge',   10.00, 150.00, 300),
(2, '3km Community Walk', 3.00, 50.00,  400),
(3, '40km Road Cycle',  40.00, 250.00, 200),
(3, '80km Endurance Cycle', 80.00, 350.00, 150),
(1, 'Under 20',          NULL,  80.00, 200);   -- age-based category: no distance
GO

-- Routes for each event
INSERT INTO dbo.Routes (EventID, RouteName, DistanceKm, ElevationGainM, MapUrl) VALUES
(1, 'Sandton CBD Loop',        10.00, 85.00,  'https://maps.raceday.co.za/routes/jhb-city-run'),
(2, 'Soweto Heritage Trail',    3.00, 40.00,  'https://maps.raceday.co.za/routes/soweto-walk'),
(3, 'Winelands Circuit',       80.00, 620.00, 'https://maps.raceday.co.za/routes/winelands-cycle');
GO

-- Sample enrolments (participants entering categories)
INSERT INTO dbo.Enrolments (ParticipantID, CategoryID, Status) VALUES
(3, 2, 'Confirmed'),  -- Lindiwe -> 10km Challenge
(3, 4, 'Confirmed'),  -- Lindiwe -> 40km Road Cycle
(4, 1, 'Confirmed'),  -- James  -> 5km Fun Run
(4, 3, 'Confirmed');  -- James  -> 3km Community Walk
GO

-- Sample results for two enrolments that have already taken place
INSERT INTO dbo.Results (EnrolmentID, FinishTime, OverallPosition, CategoryPosition, Status) VALUES
(1, '00:52:14', 34, 12, 'Finished'),
(3, '00:23:47', 8,  3,  'Finished');
GO
