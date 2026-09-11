CREATE PROCEDURE dbo.FiyatDetay_Insert @FiyatId INT, @TurId INT, @BaslangicTarihi DATE, @BitisTarihi DATE, @SayfaBasiFiyat DECIMAL(18,4)
AS
BEGIN
    INSERT INTO dbo.FiyatDetaylari (FiyatId, TurId, BaslangicTarihi, BitisTarihi, SayfaBasiFiyat)
    VALUES (@FiyatId, @TurId, @BaslangicTarihi, @BitisTarihi, @SayfaBasiFiyat);
    SELECT CAST(SCOPE_IDENTITY() AS INT) AS Id;
END
