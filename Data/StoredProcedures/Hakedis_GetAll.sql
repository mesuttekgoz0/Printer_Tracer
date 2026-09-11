CREATE PROCEDURE dbo.Hakedis_GetAll
AS
BEGIN
    SELECT
        h.Id, h.Number, h.TedarikciAd, h.CreatedUtc, h.PeriodStart, h.PeriodEnd,
        COUNT(l.Id) AS PrinterCount,
        ISNULL(SUM(CAST(ISNULL(l.Pages, 0) AS BIGINT)), 0) AS TotalPages,
        ISNULL(SUM(l.Amount), 0) AS TotalAmount
    FROM dbo.Hakedisler h
    LEFT JOIN dbo.HakedisLines l ON l.HakedisId = h.Id
    GROUP BY h.Id, h.Number, h.TedarikciAd, h.CreatedUtc, h.PeriodStart, h.PeriodEnd
    ORDER BY h.CreatedUtc DESC;
END
