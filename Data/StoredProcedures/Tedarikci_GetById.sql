CREATE PROCEDURE dbo.Tedarikci_GetById @Id INT
AS
BEGIN
    SELECT Id, Ad, [Not] FROM dbo.Tedarikciler WHERE Id = @Id;
END
