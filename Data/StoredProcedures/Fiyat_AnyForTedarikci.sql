CREATE PROCEDURE dbo.Fiyat_AnyForTedarikci @TedarikciId INT
AS
BEGIN
    SELECT CAST(CASE WHEN EXISTS (SELECT 1 FROM dbo.Fiyatlar WHERE TedarikciId = @TedarikciId) THEN 1 ELSE 0 END AS BIT) AS Found;
END
