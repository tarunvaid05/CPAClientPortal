-- Run this in SQL Server Management Studio against your CPAClientPortal database
-- Creates/ensures Admin role and an admin user (admin@gmail.com / adminpassword) exist

USE [CPAClientPortal];
GO

SET NOCOUNT ON;

-- Ensure required SET options for tables with filtered indexes/computed columns
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;

DECLARE @Email NVARCHAR(256) = N'admin@gmail.com';
DECLARE @UserName NVARCHAR(256) = @Email;
DECLARE @NormalizedEmail NVARCHAR(256) = UPPER(@Email);
DECLARE @NormalizedUserName NVARCHAR(256) = UPPER(@UserName);

-- Precomputed ASP.NET Core Identity v3 password hash for 'adminpassword'
DECLARE @PasswordHash NVARCHAR(MAX) = N'AQAAAAIAAYagAAAAEGtsMTJkMxQK6yRIOf/wu+Tg1J8ZNnT2zXdwr3vZNjbIaKBxZsrT4C8Q+HgIWsuBDQ==';

-- Ensure Admin role exists
DECLARE @RoleId NVARCHAR(450) = (SELECT TOP 1 Id FROM dbo.AspNetRoles WHERE NormalizedName = N'ADMIN');
IF @RoleId IS NULL
BEGIN
    SET @RoleId = CONVERT(NVARCHAR(450), NEWID());
    INSERT INTO dbo.AspNetRoles (Id, [Name], [NormalizedName], [ConcurrencyStamp])
    VALUES (@RoleId, N'Admin', N'ADMIN', CONVERT(NVARCHAR(450), NEWID()));
END

-- Ensure admin user exists
DECLARE @ExistingUserId NVARCHAR(450) = (SELECT TOP 1 Id FROM dbo.AspNetUsers WHERE NormalizedEmail = @NormalizedEmail);
IF @ExistingUserId IS NULL
BEGIN
    DECLARE @UserId NVARCHAR(450) = CONVERT(NVARCHAR(450), NEWID());
    DECLARE @SecurityStamp NVARCHAR(450) = CONVERT(NVARCHAR(450), NEWID());
    DECLARE @ConcurrencyStamp NVARCHAR(450) = CONVERT(NVARCHAR(450), NEWID());

    -- Insert with or without custom columns based on schema
    IF COL_LENGTH('dbo.AspNetUsers', 'FirstName') IS NOT NULL
    BEGIN
        INSERT INTO dbo.AspNetUsers
        (
            Id, UserName, NormalizedUserName, Email, NormalizedEmail,
            EmailConfirmed, PasswordHash, SecurityStamp, ConcurrencyStamp,
            PhoneNumber, PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnd,
            LockoutEnabled, AccessFailedCount,
            FirstName, LastName, ClientType, IsActive, ProfilePictureUrl
        )
        VALUES
        (
            @UserId, @UserName, @NormalizedUserName, @Email, @NormalizedEmail,
            1, @PasswordHash, @SecurityStamp, @ConcurrencyStamp,
            NULL, 0, 0, NULL,
            1, 0,
            N'Admin', N'User', N'Admin', 1, N''
        );
    END
    ELSE
    BEGIN
        INSERT INTO dbo.AspNetUsers
        (
            Id, UserName, NormalizedUserName, Email, NormalizedEmail,
            EmailConfirmed, PasswordHash, SecurityStamp, ConcurrencyStamp,
            PhoneNumber, PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnd,
            LockoutEnabled, AccessFailedCount
        )
        VALUES
        (
            @UserId, @UserName, @NormalizedUserName, @Email, @NormalizedEmail,
            1, @PasswordHash, @SecurityStamp, @ConcurrencyStamp,
            NULL, 0, 0, NULL,
            1, 0
        );
    END

    SET @ExistingUserId = @UserId;
END

-- Ensure user is in Admin role
IF NOT EXISTS (
    SELECT 1 FROM dbo.AspNetUserRoles WHERE UserId = @ExistingUserId AND RoleId = @RoleId
)
BEGIN
    INSERT INTO dbo.AspNetUserRoles (UserId, RoleId)
    VALUES (@ExistingUserId, @RoleId);
END

-- Optional: Clear any existing lockouts or failed counts
UPDATE dbo.AspNetUsers
SET LockoutEnd = NULL,
    AccessFailedCount = 0
WHERE Id = @ExistingUserId;

PRINT 'Admin user and role are ensured. You can log in as admin@gmail.com / adminpassword';

SET NOCOUNT OFF;
GO
