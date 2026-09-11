CREATE PROCEDURE dbo.Printer_Insert @Name NVARCHAR(100), @IpAddress NVARCHAR(45), @TurId INT = NULL, @TedarikciId INT = NULL
AS
BEGIN
    INSERT INTO dbo.Printers (Name, IpAddress, Model, TurId, TedarikciId) VALUES (@Name, @IpAddress, NULL, @TurId, @TedarikciId);
    SELECT CAST(SCOPE_IDENTITY() AS INT) AS Id;
END
