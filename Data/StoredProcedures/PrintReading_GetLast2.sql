CREATE PROCEDURE dbo.PrintReading_GetLast2 @PrinterId INT
AS
BEGIN
    SELECT TOP (2) PageCount, TimestampUtc FROM dbo.PrintReadings WHERE PrinterId = @PrinterId ORDER BY TimestampUtc DESC;
END
