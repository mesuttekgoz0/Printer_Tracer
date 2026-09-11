CREATE PROCEDURE dbo.PrintReading_GetEarliest @PrinterId INT
AS
BEGIN
    SELECT TOP (1) PageCount, TimestampUtc FROM dbo.PrintReadings WHERE PrinterId = @PrinterId ORDER BY TimestampUtc ASC;
END
