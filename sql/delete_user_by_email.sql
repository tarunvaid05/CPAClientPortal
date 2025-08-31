-- Run this in SQL Server Management Studio against your CPAClientPortal database
-- Safely deletes a user (and related Identity rows) by email.

USE [CPAClientPortal];
GO

SET NOCOUNT ON;

-- Ensure required SET options for Identity tables
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;

DECLARE @Email NVARCHAR(256) = N'tarunvaid05@gmail.com'; -- Change if needed
DECLARE @NormalizedEmail NVARCHAR(256) = UPPER(@Email);

DECLARE @UserId NVARCHAR(450) = (
    SELECT TOP 1 Id FROM dbo.AspNetUsers WHERE NormalizedEmail = @NormalizedEmail
);

IF @UserId IS NULL
BEGIN
    PRINT 'No user found with email ' + @Email;
    RETURN;
END

BEGIN TRY
    BEGIN TRAN;

    -- Remove dependent rows (in case cascade delete is not configured)
    DELETE FROM dbo.AspNetUserTokens WHERE UserId = @UserId;
    DELETE FROM dbo.AspNetUserLogins WHERE UserId = @UserId;
    DELETE FROM dbo.AspNetUserClaims WHERE UserId = @UserId;
    DELETE FROM dbo.AspNetUserRoles WHERE UserId = @UserId;

    -- Finally remove the user
    DELETE FROM dbo.AspNetUsers WHERE Id = @UserId;

    COMMIT TRAN;
    PRINT 'Deleted user and related identity rows for ' + @Email;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    DECLARE @ErrMsg NVARCHAR(4000) = ERROR_MESSAGE();
    DECLARE @ErrNum INT = ERROR_NUMBER();
    RAISERROR('Failed to delete user (%s): %s', 16, 1, @Email, @ErrMsg) WITH NOWAIT;
END CATCH;

SET NOCOUNT OFF;
GO

