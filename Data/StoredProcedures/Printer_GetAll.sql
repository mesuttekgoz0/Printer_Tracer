CREATE PROCEDURE dbo.Printer_GetAll
AS
BEGIN
    SELECT p.Id, p.Name, p.IpAddress, p.Model, p.TurId, tu.Ad AS TurAd, p.TedarikciId, td.Ad AS TedarikciAd
    FROM dbo.Printers p
    LEFT JOIN dbo.Turler tu ON tu.Id = p.TurId
    LEFT JOIN dbo.Tedarikciler td ON td.Id = p.TedarikciId
    ORDER BY p.Name;
END
