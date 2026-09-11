CREATE PROCEDURE dbo.Hakedis_NextNumber @Prefix NVARCHAR(10)
AS
BEGIN
    DECLARE @Max INT;
    SELECT @Max = MAX(TRY_CAST(SUBSTRING(Number, LEN(@Prefix) + 1, 10) AS INT))
    FROM dbo.Hakedisler
    WHERE Number LIKE @Prefix + '%';
    SELECT @Prefix + RIGHT('0000' + CAST(ISNULL(@Max, 0) + 1 AS VARCHAR(10)), 4) AS NextNumber;
END
