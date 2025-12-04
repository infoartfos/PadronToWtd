#



``` sql 

CREATE PROCEDURE PR_WTD3 ( in AENTRY int, in LNNUM int, in WTCD nchar(4), in TIPO nchar(1), in CUIT nvarchar(15),
in RISK nchar(1), in PRCT float, in DFEC nvarchar(10),  in HFEC nvarchar(10) )

LANGUAGE SQLSCRIPT 
SQL SECURITY INVOKER AS

-- Variables
HAY1 int;
HAY2 int;

BEGIN

HAY1=0 ;
HAY2=0 ;

SELECT COUNT (*) INTO HAY1 FROM "WTD3" 
WHERE "AbsEntry"= :AENTRY AND "KeyPart1"= :CUIT AND "DetailType"= :TIPO ;

SELECT COUNT (*) INTO HAY2 FROM "WTD3" WHERE "AbsEntry"= :AENTRY AND "LineId"= :LNNUM ;

if HAY1=0 AND HAY2=0 then 
  INSERT INTO WTD3 ( "AbsEntry", "LineId", "WTCode", "KeyPart1", "KeyPart2", "DetailType", 
  "U_B1SYS_HighRisk", "Rate", "DateFrom", "DateTo", "DataSource", "LogInstanc" )
  VALUES ( AENTRY, LNNUM, WTCD, CUIT, '80', TIPO, RISK, PRCT, TO_DATE(DFEC), 
  CASE WHEN LENGTH(HFEC) > 5 THEN TO_DATE(HFEC) ELSE '' END, 'N', 0)  ;
END IF ;

if HAY1 > 0 then 
  UPDATE WTD3 SET "DateTo" = TO_DATE(HFEC)
  WHERE "AbsEntry"= :AENTRY AND "KeyPart1"= :CUIT AND "DetailType"= :TIPO ;
END IF ;


END ;
```