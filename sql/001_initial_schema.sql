-- MouseProfiles Cloud — initial schema
-- Run manually: mysql -h <host> -u <user> -p <database> < 001_initial_schema.sql

CREATE TABLE IF NOT EXISTS Users (
    Id            CHAR(36)     NOT NULL PRIMARY KEY,
    Email         VARCHAR(255) NOT NULL UNIQUE,
    PasswordHash  VARCHAR(255) NOT NULL,
    EmailVerified TINYINT(1)   NOT NULL DEFAULT 0,
    VerifyToken   VARCHAR(255) NULL,
    VerifyTokenExpiry DATETIME NULL,
    CreatedAt     DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS Profiles (
    Id          CHAR(36)     NOT NULL PRIMARY KEY,  -- GUID assigned by desktop app
    UserId      CHAR(36)     NOT NULL,
    Name        VARCHAR(100) NOT NULL,
    ConfigJson  LONGTEXT     NOT NULL,
    CreatedAt   DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt   DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    CONSTRAINT fk_profiles_user FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
);
