CREATE PROCEDURE dbo.SP_INSERT_WTD3_SQLSERVER
    @Entry INT,
    @Linea INT,
    @WddCode INT,
    @CUIT NVARCHAR(15),
    @Desde DATE,
    @Hasta DATE,
    @Part2 NVARCHAR(2),
    @DetType NVARCHAR(1)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.WTD3 (AbsEntry, LineId, WTCode, KeyPart1, DateFrom, DateTo, KeyPart2, DetailType)
    VALUES (@Entry, @Linea, @WddCode, @CUIT, @Desde, @Hasta, @Part2, @DetType);
END;
GO

