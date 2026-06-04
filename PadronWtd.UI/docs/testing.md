# Testing — PadronWtd.UI (Add-on SAP Business One)

## Proyectos de test

```
PadronWtd.Tests.Integration/    (net472, xUnit)
PadronWtd.Tests.UI/             (net472, xUnit + Moq)
PadronWtd.Tests.Unit/           (net8.0, xUnit + Moq — capa Application, NO toca UI)
```

| Proyecto | Framework | Dependencia SAP | Propósito |
|---|---|---|---|
| `Tests.Integration` | net472 | ✅ Sí (obligatorio) | Verificar SQL real contra HANA (INSERT, subquery LineId, TO_DATE) |
| `Tests.UI` | net472 | ❌ No (mocks) | Verificar lógica de negocio de `ProcessInfoService` |
| `Tests.Unit` | net8.0 | ❌ No | Tests existentes de capa Application (no modificados) |

---

## Cómo ejecutar

### Tests unitarios (sin SAP)

```powershell
dotnet test PadronWtd.Tests.UI\PadronWtd.Tests.UI.csproj
```

### Tests de integración (requiere SAP)

1. Copiar `PadronWtd.Tests.Integration\App.config` y completar credenciales reales de una compañía de testing.
2. Asegurarse de tener el SAP DI API instalado en la máquina.
3. Ejecutar:

```powershell
dotnet test PadronWtd.Tests.Integration\PadronWtd.Tests.Integration.csproj
```

> ⚠ **Importante:** Los tests de integración insertan registros en `WTD3` y hacen rollback al final. El test `StartAndCommitTransaction_ShouldPersistMultipleInserts` hace commit y luego limpia con DELETE directo. Usar solo contra una base de testing.

---

## Phase 1 — Integration Tests (`PadronWtd.Tests.Integration`)

### Dependencias

- `PadronWtd.UI` (net472)
- xUnit + FluentAssertions
- SAP DI API (`SAPbobsCOM`) instalado en la máquina
- Conexión a base SAP Business One configurada en `App.config`

### Fixture: `SapConnectionFixture`

Inicializa el `SimpleServiceProvider` con un logger a archivo temporal y conecta via `SapConnectionManager`.

```csharp
public class Wtd3InsertTests : IClassFixture<SapConnectionFixture>
```

### Tests

| Archivo | Test | Qué verifica |
|---|---|---|
| `Wtd3InsertTests.cs` | `InsertWtd3_NewRecord_ShouldCreateRow` | INSERT + `CheckWtd3Exists` post-insert |
| `Wtd3InsertTests.cs` | `InsertWtd3_Duplicate_ShouldReturnSuccessAndSkip` | Skip cuando ya existe (mismos datos) |
| `Wtd3InsertTests.cs` | `InsertWtd3_CheckWtd3Exists_ShouldReturnFalseAfterRollback` | Rollback descarta el registro |
| `Wtd3TransactionTests.cs` | `StartAndCommitTransaction_ShouldPersistMultipleInserts` | Commit de 2 registros + cleanup con DELETE |
| `Wtd3TransactionTests.cs` | `RollbackTransaction_ShouldDiscardAllInserts` | Rollback descarta todo |

### Cleanup

Los tests que hacen **rollback** no necesitan cleanup. El test que hace **commit** (`StartAndCommitTransaction`) ejecuta DELETE directo al final.

### `TestData.cs`

Constantes editables:

```csharp
ExistingCuit = "20000156982"    // CUIT de proveedor PL existente en la DB de testing
WtcCode = "TEST01"              // WTCode que exista en la base
AbsEntry = 1                    // AbsEntry que exista en WTD1/WTD2
```

---

## Phase 2 — Unit Tests (`PadronWtd.Tests.UI`)

### Dependencias

- `PadronWtd.UI` (net472) — solo referencia de proyecto
- xUnit + Moq + FluentAssertions
- **No requiere** SAP instalado

### Arquitectura de interfaces

Para hacer testable `ProcessInfoService` se extrajeron estas interfaces:

```
┌─────────────────────┐     ┌───────────────────────────────┐
│  ProcessInfoService │────>│  IPSaltaWtd3Repository        │
│                     │     │  ├─ InsertWtd3Direct()        │
│                     │     │  ├─ CheckWtd3Exists()         │
│                     │     │  └─ UpdateAsync()             │
│                     │     └───────────────────────────────┘
│                     │     ┌───────────────────────────────┐
│                     │────>│  ITransactionManager          │
│                     │     │  ├─ StartTransaction()        │
│                     │     │  ├─ EndTransaction(option)    │
│                     │     │  └─ InTransaction             │
│                     │     └───────────────────────────────┘
│                     │     ┌───────────────────────────────┐
│                     │────>│  IProviderChecker             │
│                     │     │  └─ CuitExists(cuit)          │
└─────────────────────┘     └───────────────────────────────┘
```

### Implementaciones reales (producción)

| Interfaz | Implementación | Dependencia |
|---|---|---|
| `IPSaltaWtd3Repository` | `PSaltaRepository` | `Company` conectado |
| `ITransactionManager` | `DITransactionManager` | Delega a `Company.StartTransaction/EndTransaction` |
| `IProviderChecker` | `SapProviderChecker` | Query OCRD vía `Recordset` |

### Constructor de test

```csharp
internal ProcessInfoService(
    IPSaltaWtd3Repository saltaRepository,
    ITransactionManager transactionManager,
    IProviderChecker providerChecker,
    ILogger logger)
```

### Tests

| Test | Mocks clave | Asserts clave |
|---|---|---|
| `Cuando_TodosLosCodigosSeInsertan_DeberiaTerminarEnEstado20` | `CheckWtd3Exists → false`, `InsertWtd3Direct → (true,"")` | `U_Estado == "20"`, `StartTransaction` 1 vez, `Commit` 1 vez |
| `Cuando_AlgunosCodigosYaExisten_DeberiaInsertarLosDemasYTerminarEn40` | `CheckWtd3Exists(COD01) → true`, `CheckWtd3Exists(COD02) → false` | `U_Estado == "40"`, `U_Notas` contiene `"COD01"`, `Commit` 1 vez |
| `Cuando_TodosLosCodigosYaExisten_DeberiaTerminarEn40SinInserciones` | `CheckWtd3Exists → true` | `InsertWtd3Direct` llamado 0 veces, `U_Estado == "40"` |
| `Cuando_FallaInsert_DeberiaHacerRollbackYTerminarEn40` | `InsertWtd3Direct → (false,"DB error")`, `InTransaction → true` tras Start | `Rollback` 1 vez, `Commit` 0 veces, `U_Estado == "40"` |
| `Cuando_NoHayConfiguracionDeImpuesto_DeberiaTerminarEn40` | No se popula `_impuestosCache` | `U_Estado == "40"`, notas exactas, transacción no iniciada |

### Cache de impuestos via reflection

Los tests evitan la dependencia con `SaltaConfigRepository` poblando `_impuestosCache` directamente via reflection:

```csharp
SetImpuestosCache("ACT1", "ALTA", ("COD01", "1"), ("COD02", "2"));
```

Esto inserta en el dictionary interno `{ "ACT1_ALTA" → [{ CodigoSap: "COD01", U_Codigo: "1" }, ...] }`.

### `SafeUpdateRecord` en tests

Cuando `_company` es null (modo test), el acceso a `_company.UserTables` falla y se usa un fallback de 253 caracteres para `U_Notas`. El record igual se actualiza via `UpdateAsync` del mock.

---

## Convenciones

- **Nombres de test:** en español descriptivo (ej. `Cuando_FallaInsert_DeberiaHacerRollbackYTerminarEn40`)
- **Assertions:** `FluentAssertions` con `Should().Be()` / `Should().Contain()`
- **Verificaciones de mocks:** `Times.Once`, `Times.Never` para transacciones
- **Data de prueba:** crear con `CreateRecord()` factory method con defaults sensibles

---

## Flujo cubierto por los tests

```
ProcessSingleRecordAsync
├─ CUIT vacío? → throw → catch → estado 40
├─ CUIT no existe en OCRD? → estado 30
├─ Sin configuración de impuestos? → estado 40
├─ Con configuración:
│  ├─ StartTransaction
│  ├─ Por cada taxItem:
│  │  ├─ ¿Ya existe en WTD3? → skip (existingCodes++)
│  │  └─ No existe → InsertWtd3Direct → ¿falla? → throw → Rollback → estado 40
│  ├─ Commit
│  ├─ ¿existingCodes.Any? → estado 40 con nota
│  └─ Todos insertados → estado 20
└─ catch general → estado 40 con mensaje de error
```
