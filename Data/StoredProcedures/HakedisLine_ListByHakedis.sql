CREATE PROCEDURE dbo.HakedisLine_ListByHakedis @HakedisId INT
AS
BEGIN
    SELECT Id, HakedisId, PrinterId, PrinterName, Model, TurId, TurAd, PreviousCounter, CurrentCounter, Pages, UnitPrice, Amount
    FROM dbo.HakedisLines WHERE HakedisId = @HakedisId ORDER BY Id;
END
