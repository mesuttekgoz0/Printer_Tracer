CREATE PROCEDURE dbo.Tedarikci_ListForHakedisSecim
AS
BEGIN
    SELECT
        t.Id, t.Ad,
        (SELECT COUNT(*) FROM dbo.Printers p WHERE p.TedarikciId = t.Id) AS PrinterCount,
        CAST(CASE WHEN EXISTS (SELECT 1 FROM dbo.Fiyatlar f WHERE f.TedarikciId = t.Id) THEN 1 ELSE 0 END AS BIT) AS HasPriceList
    FROM dbo.Tedarikciler t
    ORDER BY t.Ad;
END
