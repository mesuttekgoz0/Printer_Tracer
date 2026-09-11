CREATE PROCEDURE dbo.Fiyat_InsertMaster @TedarikciId INT, @TurId INT
AS
BEGIN
    INSERT INTO dbo.Fiyatlar (TedarikciId, TurId) VALUES (@TedarikciId, @TurId);
    SELECT CAST(SCOPE_IDENTITY() AS INT) AS Id;
END
