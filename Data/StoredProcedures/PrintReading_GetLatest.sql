CREATE PROCEDURE dbo.PrintReading_GetLatest @PrinterId INT
AS
BEGIN
    SELECT TOP (1) PageCount, TimestampUtc FROM dbo.PrintReadings WHERE PrinterId = @PrinterId ORDER BY TimestampUtc DESC;
END
