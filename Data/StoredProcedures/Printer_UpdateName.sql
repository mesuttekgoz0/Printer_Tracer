CREATE PROCEDURE dbo.Printer_UpdateName @Id INT, @Name NVARCHAR(100)
AS
BEGIN
    UPDATE dbo.Printers SET Name = @Name WHERE Id = @Id;
    SELECT @@ROWCOUNT AS Affected;
END
