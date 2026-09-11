CREATE PROCEDURE dbo.Printer_UpdateModel @Id INT, @Model NVARCHAR(250) = NULL
AS
BEGIN
    UPDATE dbo.Printers SET Model = @Model WHERE Id = @Id;
    SELECT @@ROWCOUNT AS Affected;
END
