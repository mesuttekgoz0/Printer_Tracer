CREATE PROCEDURE dbo.Printer_ListAllIpAddresses
AS
BEGIN
    SELECT IpAddress FROM dbo.Printers;
END
