# AGENTS.md — PadronWtd.UI (Add-on SAP Business One)

## Propósito

Add-on de SAP Business One que automatiza la importación de padrones de contribuyentes de la Dirección General de Rentas de Salta (Archivos TSV con CUIT, denominación, actividad económica y riesgo fiscal) y su procesamiento en la tabla de retenciones WTD3 de SAP.

## Arquitectura General

- **Target**: .NET Framework 4.7.2
- **Interfaz**: SAPbouiCOM (UI API de SAP B1) — formularios nativos de SAP
- **Datos**: SAPbobsCOM (DI API) — acceso directo a tablas de SAP HANA
- **Logging**: Archivo en `C:\ProgramData\PadronWtd\padron_import.log`
- **DI**: `SimpleServiceProvider` — contenedor manual (estático, tipo service locator)

## Estructura del Proyecto

```
PadronWtd.UI/
├── Program.cs                          # Punto de entrada: conecta UI+DI API, registra menú
├── App.cs                              # Singleton estático: SBO_Application + Company
├── AppSettings.cs                      # Lee App.config (Configuration/)
├── AppConstants.cs / MenuConstants.cs  # Constantes de UI y menús
├── Menu.cs                             # Registro y manejo de menús SAP
├── extension.xml                       # Manifiesto de add-on v1.0.9
├── PadronWtd.ard                       # Registro de add-on (x86/x64)
├── Forms/
│   ├── MainForm.cs                     # Formulario principal (3 botones)
│   └── FrmImportar.cs                  # Formulario de importación y procesamiento
├── Services/
│   ├── IImportService.cs               # Interfaz de importación
│   ├── FileImportService.cs            # Parsea TSV e inserta en @PADRON_SALTA_IMP3
│   ├── PeriodosService.cs              # Obtiene períodos activos desde @CONT_DATE_CAB/DET
│   └── ProcessInfoService.cs           # Lógica de negocio: procesa registros contra WTD3
├── Domain/
│   ├── PSaltaRecord.cs                 # Entidad: registro de @PADRON_SALTA_IMP3
│   ├── ContDetRecord.cs                # Entidad: registro de @CONT_DATE_DET
│   └── ComboItem.cs                    # Item para combobox (Value/Description)
├── Dto/
│   ├── PSaltaRecordDto.cs              # DTO sin campos de auditoría
│   ├── PSaltaCsvMapper.cs              # Lector/escritor CSV estático
│   ├── ProcessResult.cs                # Resultado del procesamiento
│   └── ImpuestoRecord.cs               # Configuración de impuesto (Inscripcion/Riesgo -> CodigoSAP)
├── Repository/
│   ├── DI/
│   │   ├── SapConnectionManager.cs     # Singleton: maneja conexión Company (servicio o UI)
│   │   ├── PSaltaRepository.cs         # CRUD sobre @PADRON_SALTA_IMP3 + inserción WTD3
│   │   ├── ContDateRepository.cs       # Consulta @CONT_DATE_CAB/DET, desactiva períodos
│   │   └── SaltaConfigRepository.cs    # Lee @COD_SALTA_CAB/DET (mapeo impuestos)
│   └── SL/
│       └── PSaltaRepository.cs         # CRUD vía Service Layer REST (P_Salta)
├── SL/
│   ├── ServiceLayerClient.cs           # Cliente HTTP REST para SAP Service Layer
│   ├── ServiceLayerClientOriginalBorrar.cs  # Versión anterior con cluster retry
│   ├── ServiceLayerPClientBorrar.cs    # Versión anterior con session management
│   ├── SapPSaltaService.cs             # Wrapper PostAsync("P_Salta", dto)
│   ├── PSaltaDto.cs                    # DTO para Service Layer
│   └── HttpDebug.cs                    # Debug de requests/responses HTTP
├── Helper/
│   ├── EncryptionHelper.cs            # NO implementado (retorna ciphertext)
│   ├── FileDialogHelper.cs            # OpenFileDialog en STA thread
│   └── SequentialId.cs                # Genera IDs únicos (ticks + GUID)
├── Logging/
│   ├── ILogger.cs                     # Interfaz: Info/Warn/Error
│   └── FileLogger.cs                  # Implementación: escribe a archivo con lock
├── DI/
│   └── SimpleServiceProvider.cs       # Service locator estático con registro de factories
└── DebugRunner/
    ├── ImportRunner.cs                # Debug: importa CSV vía Service Layer
    ├── LeerPadronRunner.cs            # Debug: conecta DI API directo y procesa
    └── LeerPadronRunnerAddOn.cs       # Debug: simula add-on con SSO
```

## Flujo de Ejecución Completo

### 1. Inicio (Program.cs)
1. Inicializa `SimpleServiceProvider` con `FileLogger` (log en `C:\ProgramData\PadronWtd\padron_import.log`).
2. Conecta con SAP GUI vía `SboGuiApi.Connect(args[0])`.
3. Obtiene `SBO_Application` (UI API) y `Company` (DI API) vía `GetDICompany()`.
4. Registra menú "Padrón Salta > Importación Padrón Salta" bajo Finanzas (menú 1536).
5. Suscribe eventos `MenuEvent` y `AppEvent` (ShutDown, CompanyChanged, ServerTermination → `TerminateAddon`).
6. Inicia `Application.Run()` (bucle de mensajes de WinForms).

### 2. Apertura del Formulario (MainForm.cs)
Al hacer clic en "Importación Padrón Salta":
1. Crea formulario SAP `frmPadron` con:
   - "Mantenimiento de Fecha" → busca y activa menú SAP "Fechas de Procesamiento SALTA"
   - "Mantenimiento de Impuestos" → busca y activa menú SAP "Parametros Padrón SALTA"
   - "Importar y procesar" → abre `FrmImportar`

### 3. Importación y Procesamiento (FrmImportar.cs)
#### UI
- Formulario SAP `FrmImpType` con:
  - Combo: período activo (cargado desde `@CONT_DATE_CAB`/`@CONT_DATE_DET` vía `PeriodosService`)
  - Campo de texto: ruta del archivo
  - Botón "..." → `OpenFileDialog` en thread STA
  - Botón "Importar y Procesar" (visible solo si no hay errores)
  - Botón "Reprocesar Errores" (visible solo si hay errores)
  - Labels de resultados
- Timer de 500ms para revisar cola de archivos (solución a problema de foco SAP)

#### Import (FileImportService.cs)
1. Parsea archivo TSV (tab-separado, salta header que empieza con "CUIT").
2. Por cada línea: extrae CUIT (col 0), Inscripción (col 2), Riesgo (col 3).
3. Genera `PSaltaRecord` con `SequentialId.Generate()` como Code.
4. Borra registros previos del mismo año/periodo en `@PADRON_SALTA_IMP3`.
5. Bulk insert en SAP HANA vía `BuildHanaInsertBatch` (INSERT con UNION ALL, 500 por lote, código secuencial numérico desde `MAX(Code)+1`).
6. Estado inicial: `'10'`.

#### Procesamiento SAP (ProcessInfoService.cs)
1. **Carga caché de impuestos**: JOIN `@COD_SALTA_CAB` (U_Tipo_Insc, U_Riesgo) + `@COD_SALTA_DET` (U_CodigoSAP, U_Codigo) donde activos. Clave: `"{INSCRIPCION}_{RIESGO}"`.
2. **Calcula totales**: suma de estados (Importado, 10, Procesado, 20, No Encontrado, 30, Error, 40).
3. **Marca proveedores inexistentes**: UPDATE a estado '30' donde CUIT no existe en `OCRD` con CardCode LIKE 'PL%'.
4. **Procesa registros**: para cada registro en estado 'Importado', '10', 'Error', '40':
   - Verifica que CUIT exista en `OCRD` (si no → estado '30').
   - Busca códigos de retención en caché por Inscripción+Riesgo (si no → estado '40').
   - Para cada impuesto configurado: llama a `InsertWtd3Direct`:
     - Verifica si ya existe WTD3 con mismo AbsEntry + WTCode + CUIT + DateFrom (si existe → skip).
     - Obtiene próximo LineId (`MAX(LineId)+1`).
     - INSERT en WTD3 con campos: AbsEntry, LineId, WTCode, KeyPart1 (CUIT), KeyPart2 ('80'), DateFrom, DateTo, DetailType ('A'), DataSource ('M'), UpdateDate (NOW).
   - Si OK → estado '20'; si error → estado '40'.
5. **Desactiva período**: UPDATE `@CONT_DATE_DET` SET U_Activo = 'NO'.

#### Reprocesamiento
1. Resetea registros con error: UPDATE a estado '10'.
2. Vuelve a ejecutar el procesamiento solo para esos registros.

## Tablas SAP Utilizadas

| Tabla SAP | Propósito | Campos Clave |
|---|---|---|
| `@PADRON_SALTA_IMP3` | Registros importados | Code, Name, U_Anio, U_Padron, U_Cuit, U_Inscripcion, U_Riesgo, U_Notas, U_Procesado, U_Estado |
| `@CONT_DATE_CAB` | Cabecera de períodos | Code, U_Detalle (año) |
| `@CONT_DATE_DET` | Detalle de períodos | Code, LineId, U_Periodo, U_Desde, U_Hasta, U_Activo |
| `@COD_SALTA_CAB` | Configuración impuestos (cabecera) | DocEntry, U_Tipo_Insc, U_Riesgo, U_Activo |
| `@COD_SALTA_DET` | Configuración impuestos (detalle) | DocEntry, LineId, U_CodigoSAP, U_Codigo, U_Activo |
| `OCRD` | Socios de negocio | LicTradNum (CUIT), CardCode |
| `WTD3` | Datos de retención | AbsEntry, LineId, WTCode, KeyPart1 (CUIT), DateFrom, DateTo, KeyPart2, DetailType |

## Estados de Registro

| Código | Significado |
|---|---|
| `10` | Importado (recién cargado) |
| `20` | Procesado OK |
| `30` | Proveedor no encontrado en OCRD |
| `40` | Error (configuración no encontrada u otro error) |

## Service Layer (SL)

El proyecto incluye dos implementaciones para acceso REST vía SAP Service Layer:
- Un cliente activo (`ServiceLayerClient.cs`) con manejo de cookies B1SESSION y ROUTEID.
- Versiones anteriores/descartadas (`ServiceLayerClientOriginalBorrar.cs`, `ServiceLayerPClientBorrar.cs`) con reintentos por cluster failover y session timeout.

## Debug Runners

Tres runners para depuración sin SAP GUI:
- `ImportRunner.cs` — conecta vía Service Layer, lee CSV, inserta en P_Salta.
- `LeerPadronRunner.cs` — conecta DI API directo (con credenciales hardcodeadas), procesa padrón.
- `LeerPadronRunnerAddOn.cs` — simula SSO (conecta a SAP GUI en ejecución).

## Packager (tool/Packager.ps1)

Script PowerShell que:
1. Compila x86 y x64 con MSBuild.
2. Calcula hash MD5 de cada ejecutable.
3. Inyecta hashes en `PadronWtd.ard`.
4. Genera ZIP con extension.xml, .ard y binarios.

## Notas Técnicas

- **EncryptionHelper.Decrypt** no está implementado (retorna el texto cifrado tal cual).
- **SequentialId** genera IDs de 32 caracteres (16 ticks hex + 16 GUID hex).
- El flag `DEBUG = false` en `Program.cs` permite modo standalone sin SAP.
- Las fechas en WTD3 se formatean como `yyyyMMdd` para TO_DATE de HANA.
- Los strings se sanitizan (SQL injection mitigation básica: `reemplazar ' por ''`).
- El bulk insert usa `UNION ALL` con `FROM DUMMY` (sintaxis HANA).
