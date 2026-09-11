CREATE PROCEDURE dbo.Tedarikci_Update @Id INT, @Ad NVARCHAR(150), @Not NVARCHAR(500) = NULL
AS
BEGIN
    UPDATE dbo.Tedarikciler SET Ad = @Ad, [Not] = @Not WHERE Id = @Id;
    SELECT @@ROWCOUNT AS Affected;
END
