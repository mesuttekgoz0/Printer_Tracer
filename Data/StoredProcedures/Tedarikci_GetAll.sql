CREATE PROCEDURE dbo.Tedarikci_GetAll
AS
BEGIN
    SELECT
        t.Id, t.Ad, t.[Not],
        (SELECT COUNT(*) FROM dbo.Printers p WHERE p.TedarikciId = t.Id) AS PrinterCount,
        (SELECT COUNT(*) FROM dbo.Fiyatlar f WHERE f.TedarikciId = t.Id) AS FiyatSayisi,
        (SELECT COUNT(*) FROM dbo.FiyatDetaylari fd JOIN dbo.Fiyatlar f2 ON f2.Id = fd.FiyatId WHERE f2.TedarikciId = t.Id) AS DetaySayisi
    FROM dbo.Tedarikciler t
    ORDER BY t.Ad;
END
