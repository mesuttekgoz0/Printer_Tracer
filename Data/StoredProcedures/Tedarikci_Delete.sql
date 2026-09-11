CREATE PROCEDURE dbo.Tedarikci_Delete @Id INT
AS
BEGIN
    DELETE FROM dbo.Tedarikciler WHERE Id = @Id;
    SELECT @@ROWCOUNT AS Affected;
END
