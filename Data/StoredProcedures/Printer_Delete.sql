CREATE PROCEDURE dbo.Printer_Delete @Id INT
AS
BEGIN
    DELETE FROM dbo.Printers WHERE Id = @Id;
    SELECT @@ROWCOUNT AS Affected;
END
