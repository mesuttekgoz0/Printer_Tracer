CREATE PROCEDURE dbo.FiyatDetay_GetById @Id INT
AS
BEGIN
    SELECT Id, FiyatId, TurId, BaslangicTarihi, BitisTarihi, SayfaBasiFiyat
    FROM dbo.FiyatDetaylari WHERE Id = @Id;
END
