CREATE PROCEDURE dbo.Hakedis_Delete @Id INT
AS
BEGIN
    DELETE FROM dbo.Hakedisler WHERE Id = @Id;
    SELECT @@ROWCOUNT AS Affected;
END
