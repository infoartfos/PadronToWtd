
-- Poc
CREATE TABLE dbo.WTD3
(
    AbsEntry INT,
    LineId INT,
    WTCode INT,
    KeyPart1 NVARCHAR(15), -- CUIT
    DateFrom DATE,
    DateTo DATE,
    KeyPart2 NVARCHAR(2),
    DetailType NVARCHAR(1)
);



CREATE TABLE dbo.PROCESS_LOG
(
    Id INT IDENTITY(1,1) PRIMARY KEY,
    RunId INT NOT NULL,
    CardCode NVARCHAR(50) NULL,
    CardName NVARCHAR(250) NULL,
    CUIT NVARCHAR(20) NULL,
    Updated CHAR(2) NOT NULL, -- 'SI' or 'NO'
    Details NVARCHAR(1000) NULL,
    ProcessedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    ProcessedBy NVARCHAR(100) NULL
);



CREATE TABLE dbo.IMPUESTOS
(
    Id INT IDENTITY(1,1) PRIMARY KEY,
    ConditionName NVARCHAR(100) NULL, -- e.g. Inscripción, Riesgo, etc.
    Inscripcion NVARCHAR(100) NULL,
    Riesgo NVARCHAR(100) NULL,
    Convenio NVARCHAR(100) NULL,
    Code NVARCHAR(50) NOT NULL,       -- RQ35, RQ37, ...
    Detail NVARCHAR(250) NULL,
    WtCode INT NULL,                  -- maps to WTD.WTCode if needed
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);



CREATE TABLE dbo.RUNS
(
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(100) NULL,
    Year INT NULL,
    DateFrom DATE NOT NULL,
    DateTo DATE NOT NULL,
    Active CHAR(2) NOT NULL DEFAULT 'NO', -- 'SI' o 'NO'
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy NVARCHAR(100) NULL
);

CREATE UNIQUE INDEX UX_RUNS_ACTIVE
ON dbo.RUNS(Active)
WHERE Active = 'SI';
-- Alternatively enforce via trigger/constraint logic if SQL Server version doesn't support filtered unique index in target env.


-- PADRON_SALTA: tabla que contiene los datos del padrón importado
CREATE TABLE dbo.PADRON_SALTA
(
    Id INT IDENTITY(1,1) PRIMARY KEY,
    RunId INT NOT NULL,              -- FK a Runs.Id
    LineNumber INT NOT NULL,
    RawLine NVARCHAR(MAX) NULL,
    CUIT NVARCHAR(20) NOT NULL,
    Denominacion NVARCHAR(250) NULL,
    ActividadEconomica NVARCHAR(50) NULL,
    NivelRiesgo NVARCHAR(50) NULL,
    InsertedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    ImportedBy NVARCHAR(100) NULL
);
