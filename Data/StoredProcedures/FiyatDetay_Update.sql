CREATE PROCEDURE dbo.FiyatDetay_Update @Id INT, @BaslangicTarihi DATE, @BitisTarihi DATE, @SayfaBasiFiyat DECIMAL(18,4)
AS
BEGIN
    UPDATE dbo.FiyatDetaylari
    SET BaslangicTarihi = @BaslangicTarihi, BitisTarihi = @BitisTarihi, SayfaBasiFiyat = @SayfaBasiFiyat
    WHERE Id = @Id;
    SELECT @@ROWCOUNT AS Affected;
END
