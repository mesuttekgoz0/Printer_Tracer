CREATE PROCEDURE dbo.Hakedis_GetById @Id INT
AS
BEGIN
    SELECT Id, Number, TedarikciId, TedarikciAd, CreatedUtc, PeriodStart, PeriodEnd, Note
    FROM dbo.Hakedisler WHERE Id = @Id;
END
