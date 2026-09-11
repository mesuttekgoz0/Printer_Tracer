CREATE PROCEDURE dbo.Printer_UpdateTur @Id INT, @TurId INT = NULL
AS
BEGIN
    UPDATE dbo.Printers SET TurId = @TurId WHERE Id = @Id;
    SELECT @@ROWCOUNT AS Affected;
END
