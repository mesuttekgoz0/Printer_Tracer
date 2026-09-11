CREATE PROCEDURE dbo.PrintReading_CountByPrinter @PrinterId INT
AS
BEGIN
    SELECT COUNT(*) FROM dbo.PrintReadings WHERE PrinterId = @PrinterId;
END
