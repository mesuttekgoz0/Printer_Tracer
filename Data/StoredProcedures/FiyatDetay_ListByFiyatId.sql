CREATE PROCEDURE dbo.FiyatDetay_ListByFiyatId @FiyatId INT
AS
BEGIN
    SELECT Id, FiyatId, TurId, BaslangicTarihi, BitisTarihi, SayfaBasiFiyat
    FROM dbo.FiyatDetaylari
    WHERE FiyatId = @FiyatId
    ORDER BY BaslangicTarihi DESC, Id DESC;
END
