CREATE PROCEDURE dbo.HakedisLine_GetLastForPrinter @PrinterId INT
AS
BEGIN
    SELECT TOP (1) l.CurrentCounter, h.PeriodEnd
    FROM dbo.HakedisLines l
    JOIN dbo.Hakedisler h ON h.Id = l.HakedisId
    WHERE l.PrinterId = @PrinterId
    ORDER BY h.CreatedUtc DESC;
END
