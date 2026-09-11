CREATE PROCEDURE dbo.HakedisLine_Insert
    @HakedisId INT, @PrinterId INT = NULL, @PrinterName NVARCHAR(100), @Model NVARCHAR(250) = NULL,
    @TurId INT = NULL, @TurAd NVARCHAR(50) = NULL, @PreviousCounter BIGINT, @CurrentCounter BIGINT,
    @Pages BIGINT = NULL, @UnitPrice DECIMAL(18,4), @Amount DECIMAL(18,2)
AS
BEGIN
    INSERT INTO dbo.HakedisLines
        (HakedisId, PrinterId, PrinterName, Model, TurId, TurAd, PreviousCounter, CurrentCounter, Pages, UnitPrice, Amount)
    VALUES
        (@HakedisId, @PrinterId, @PrinterName, @Model, @TurId, @TurAd, @PreviousCounter, @CurrentCounter, @Pages, @UnitPrice, @Amount);
END
