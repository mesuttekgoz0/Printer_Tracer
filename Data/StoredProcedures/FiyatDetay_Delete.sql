CREATE PROCEDURE dbo.FiyatDetay_Delete @Id INT
AS
BEGIN
    DELETE FROM dbo.FiyatDetaylari WHERE Id = @Id;
    SELECT @@ROWCOUNT AS Affected;
END
