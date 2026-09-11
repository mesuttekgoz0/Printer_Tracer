CREATE PROCEDURE dbo.Hakedis_Insert
    @Number NVARCHAR(20), @CreatedUtc DATETIME2, @TedarikciId INT = NULL, @TedarikciAd NVARCHAR(150) = NULL,
    @PeriodStart DATE, @PeriodEnd DATE, @Note NVARCHAR(500) = NULL
AS
BEGIN
    INSERT INTO dbo.Hakedisler (Number, CreatedUtc, TedarikciId, TedarikciAd, PeriodStart, PeriodEnd, Note)
    VALUES (@Number, @CreatedUtc, @TedarikciId, @TedarikciAd, @PeriodStart, @PeriodEnd, @Note);
    SELECT CAST(SCOPE_IDENTITY() AS INT) AS Id;
END
