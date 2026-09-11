CREATE PROCEDURE dbo.FiyatDetay_ListByTedarikci @TedarikciId INT
AS
BEGIN
    SELECT fd.Id, fd.FiyatId, fd.TurId, fd.BaslangicTarihi, fd.BitisTarihi, fd.SayfaBasiFiyat
    FROM dbo.FiyatDetaylari fd
    JOIN dbo.Fiyatlar f ON f.Id = fd.FiyatId
    WHERE f.TedarikciId = @TedarikciId
    ORDER BY fd.FiyatId, fd.BaslangicTarihi DESC, fd.Id DESC;
END
