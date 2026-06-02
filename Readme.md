# PadronWtd

Importador y procesador de padrones de contribuyentes de la Dirección General de Rentas de Salta, integrado como add-on de SAP Business One para la gestión de retenciones impositivas (WTD).

## Descripción

PadronWtd automatiza la importación de archivos CSV/TSV con datos del padrón de contribuyentes (CUIT, denominación, actividades económicas, nivel de riesgo fiscal) y su procesamiento en SAP Business One, creando o actualizando registros de retenciones (WTD3) según reglas impositivas configurables.

## Arquitectura

El proyecto está organizado en una arquitectura de capas (Clean Architecture) con proyectos .NET 8.0, y convive con un add-on legacy para SAP Business One en .NET Framework 4.7.2:

| Proyecto | Tecnología | Propósito |
|---|---|---|
| `PadronWtd.Domain` | .NET 8.0 | Entidades del dominio |
| `PadronWtd.Application` | .NET 8.0 | Casos de uso e interfaces |
| `PadronWtd.Infrastructure` | .NET 8.0 | Acceso a datos (EF Core, Dapper, SQL Server) e importación CSV |
| `PadronWtd.Cli` | .NET 8.0 | Interfaz de línea de comandos (importar y procesar) |
| `PadronWtd.UiWin` | .NET 8.0 WPF | Interfaz de escritorio (en desarrollo) |
| `PadronWtd.UI` | .NET Framework 4.7.2 | Add-on WinForms para SAP Business One (código productivo actual) |
| `PadronWtd.Tests.Unit` | .NET 8.0 | Pruebas unitarias con xUnit |

## Tecnologías

- **Lenguaje**: C#
- **.NET**: 8.0 (nuevos proyectos) / 4.7.2 (add-on legacy)
- **Interfaz gráfica**: Windows Forms (legacy), WPF (nuevo)
- **Base de datos**: SQL Server (nuevo) / SAP HANA vía DI API (legacy)
- **ORM**: Entity Framework Core 8.0, Dapper 2.1
- **Integración SAP**: SAP Business One DI API (COM), UI API (COM), Service Layer (REST)
- **CSV**: CsvHelper 33.1
- **Pruebas**: xUnit, FluentAssertions, Moq, coverlet

## Flujo de trabajo (add-on legacy)

1. El usuario abre el formulario "Importar y procesar" desde el menú de SAP Business One.
2. Selecciona un archivo TSV con el formato:
   ```
   CUIT    DENOMINACION    ACTIVIDADES_ECONOMICAS    NIVEL_RIESGO_FISCAL
   20010339023    BATELMAN SIMON Y DANIEL    CM    RA
   ```
3. El sistema importa los registros a la tabla de usuario SAP `@PADRON_SALTA_IMP3`.
4. Procesa cada registro: busca el socio de negocio en `OCRD` por CUIT, determina los códigos de retención según la configuración de impuestos (tablas `@COD_SALTA_CAB/DET`), y crea/actualiza registros en `WTD3`.
5. Muestra resumen de resultados y permite reprocesar errores.

## Base de datos

### Tablas de usuario SAP (add-on legacy)

- `@PADRON_SALTA_IMP3` — registros importados del padrón
- `@CONT_DATE_CAB/DET` — períodos de procesamiento
- `@COD_SALTA_CAB/DET` — configuración de códigos de retención

### Tablas SQL Server (nuevo proyecto)

- `dbo.PADRON_SALTA` — datos importados del padrón
- `dbo.RUNS` — ejecuciones de procesamiento
- `dbo.IMPUESTOS` — configuración de impuestos
- `dbo.PROCESS_LOG` — auditoría de procesamiento
- `dbo.WTD3` — datos de retenciones

## Requisitos

- Visual Studio 2022 o .NET 8.0 SDK
- Para el add-on legacy: SAP Business One cliente (COM references `SAPbobsCOM`, `SAPbouiCOM`) y .NET Framework 4.7.2

## Compilar

```bash
dotnet build PadronWtd.sln
```

## Ejecutar (CLI)

```bash
# Importar archivo de padrón
dotnet run --project PadronWtd.Cli -- import <archivo.tsv>

# Procesar una ejecución
dotnet run --project PadronWtd.Cli -- process <runId>
```

## Probar

```bash
dotnet test PadronWtd.Tests.Unit
```

## Log

El add-on legacy escribe logs en:
```
C:\ProgramData\PadronWtd\padron_import.log
```
