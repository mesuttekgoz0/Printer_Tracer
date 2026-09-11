CREATE PROCEDURE dbo.PrintReading_Insert @PrinterId INT, @PageCount BIGINT, @TimestampUtc DATETIME2
AS
BEGIN
    INSERT INTO dbo.PrintReadings (PrinterId, PageCount, TimestampUtc) VALUES (@PrinterId, @PageCount, @TimestampUtc);
END
