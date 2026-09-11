CREATE PROCEDURE dbo.PrintReading_ListBeforeUtc @PrinterId INT, @EndUtc DATETIME2
AS
BEGIN
    SELECT TimestampUtc, PageCount FROM dbo.PrintReadings
    WHERE PrinterId = @PrinterId AND TimestampUtc < @EndUtc
    ORDER BY TimestampUtc ASC;
END
