CREATE PROCEDURE dbo.PrintReading_AggregateAll
AS
BEGIN
    SELECT PrinterId, COUNT(*) AS Cnt, MAX(TimestampUtc) AS LatestUtc
    FROM dbo.PrintReadings
    GROUP BY PrinterId;
END
