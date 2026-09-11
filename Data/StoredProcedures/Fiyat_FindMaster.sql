CREATE PROCEDURE dbo.Fiyat_FindMaster @TedarikciId INT, @TurId INT
AS
BEGIN
    SELECT Id FROM dbo.Fiyatlar WHERE TedarikciId = @TedarikciId AND TurId = @TurId;
END
