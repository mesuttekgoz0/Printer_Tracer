CREATE PROCEDURE dbo.FiyatDetay_CountByFiyatId @FiyatId INT
AS
BEGIN
    SELECT COUNT(*) FROM dbo.FiyatDetaylari WHERE FiyatId = @FiyatId;
END
