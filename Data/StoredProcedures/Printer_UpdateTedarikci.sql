CREATE PROCEDURE dbo.Printer_UpdateTedarikci @Id INT, @TedarikciId INT = NULL
AS
BEGIN
    UPDATE dbo.Printers SET TedarikciId = @TedarikciId WHERE Id = @Id;
    SELECT @@ROWCOUNT AS Affected;
END
