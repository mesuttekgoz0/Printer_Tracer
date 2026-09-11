CREATE PROCEDURE dbo.Fiyat_DeleteMaster @Id INT
AS
BEGIN
    DELETE FROM dbo.Fiyatlar WHERE Id = @Id;
    SELECT @@ROWCOUNT AS Affected;
END
