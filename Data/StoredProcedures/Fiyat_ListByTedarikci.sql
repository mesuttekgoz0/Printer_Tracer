CREATE PROCEDURE dbo.Fiyat_ListByTedarikci @TedarikciId INT
AS
BEGIN
    SELECT f.Id, f.TedarikciId, f.TurId, tu.Ad AS TurAd
    FROM dbo.Fiyatlar f
    LEFT JOIN dbo.Turler tu ON tu.Id = f.TurId
    WHERE f.TedarikciId = @TedarikciId
    ORDER BY f.TurId;
END
