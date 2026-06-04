# Casos de Actualización en WTD3

## Referencia del Código

La lógica de inserción en `WTD3` se encuentra en dos métodos:

- `PSaltaRepository.InsertWtd3Direct` (`Repository/DI/PSaltaRepository.cs:387`) — ejecuta el INSERT físico
- `ProcessInfoService.ProcessSingleRecordAsync` (`Services/ProcessInfoService.cs:110`) — orquesta el procesamiento por registro

## Estructura de Configuración

La relación entre Inscripción/Riesgo y los códigos de retención SAP se define en dos tablas de usuario:

```
@COD_SALTA_CAB (cabecera)
├── DocEntry       (PK)
├── U_Tipo_Insc    (ej: CM, JU, XX)
├── U_Riesgo       (ej: RA, SR, EE)
└── U_Activo       (SI/NO)

@COD_SALTA_DET (detalle, FK por DocEntry)
├── DocEntry       (FK → @COD_SALTA_CAB)
├── LineId
├── U_CodigoSAP    (código de retención SAP, ej: RQ35, RQ37)
├── U_Codigo       (entero → AbsEntry en WTD3)
└── U_Activo       (SI/NO)
```

Una misma cabecera (`Inscripcion + Riesgo`) puede tener **múltiples detalles** activos, cada uno con un `U_CodigoSAP` y `U_Codigo` diferente.

## Campos Insertados en WTD3

```sql
INSERT INTO "WTD3" (
    "AbsEntry",         -- U_Codigo del detalle (int)
    "LineId",           -- MAX(LineId)+1 para ese AbsEntry
    "WTCode",           -- U_CodigoSAP del detalle
    "KeyPart1",         -- CUIT del contribuyente
    "KeyPart2",         -- '80' (fijo)
    "DateFrom",         -- Fecha de inicio del período
    "DateTo",           -- Fecha de fin del período
    "DetailType",       -- 'A' (fijo)
    "DataSource",       -- 'M' (fijo)
    "UpdateDate"        -- NOW()
)
```

## Lógica de Verificación Pre-Insert

Antes de insertar se ejecuta esta consulta (código en `PSaltaRepository.cs:405-411`):

```sql
SELECT COUNT(*) AS CANT
FROM "WTD3"
WHERE "AbsEntry" = {entry}
  AND "WTCode"   = '{wddCode}'
  AND "KeyPart1" = '{cuit}'
  AND "DateFrom" = TO_DATE('{fDesde}', 'YYYYMMDD')
```

Si `CANT > 0` → se **salta** el insert (no hay error, se considera exitoso).

---

## Caso 1: Insert limpio (no existe registro previo)

**Escenario**: Es la primera vez que se procesa un CUIT para un período y combinación de impuestos.

**Flujo**:
1. `SELECT COUNT(*)` → devuelve 0
2. `SELECT IFNULL(MAX("LineId"), -1) + 1` → obtiene el próximo LineId disponible para ese `AbsEntry`
3. INSERT en `WTD3` con todos los campos
4. Retorna `success = true`

**Resultado**: El registro se crea en WTD3. El `PadronRecord` se actualiza a estado `20` (Procesado OK).

---

## Caso 2: Registro existente con mismo código y fecha (skip)

**Escenario**: Se vuelve a ejecutar la importación para el mismo período (o se reprocesa) y ya existe un registro exactamente igual en WTD3.

**Flujo**:
1. `SELECT COUNT(*)` → devuelve `> 0`
2. Log: `"WTD3 ya existe para CUIT:{cuit} WTCode:{wddCode} Desde:{fDesde} - Skipping"`
3. Retorna `success = true` (no es un error)

**Resultado**: El insert se omite, no se genera error, no hay duplicados. El `PadronRecord` continúa procesándose con los demás códigos.

**Importante**: La lógica **no actualiza** `DateTo` ni ningún otro campo del registro existente. Sólo salta.

---

## Caso 3: Múltiples códigos de retención configurados

**Escenario**: Para una misma combinación de `Inscripcion + Riesgo` existen varios detalles activos en `@COD_SALTA_DET`. Por ejemplo:

```
@COD_SALTA_CAB:
DocEntry=1, U_Tipo_Insc=CM, U_Riesgo=RA

@COD_SALTA_DET:
DocEntry=1, LineId=1, U_CodigoSAP=RQ35, U_Codigo=115
DocEntry=1, LineId=2, U_CodigoSAP=RQ37, U_Codigo=116
```

**Caché generado** (`LoadImpuestosCacheAsync`):
```
key = "CM_RA" → [
    { CodigoSap="RQ35", U_Codigo="115" },
    { CodigoSap="RQ37", U_Codigo="116" }
]
```

**Flujo** (`ProcessSingleRecordAsync:132-156`):
1. Itera sobre cada `ImpuestoCacheItem` en el mismo `PadronRecord`
2. Para `RQ35` con `AbsEntry=115`:
   - Verifica si ya existe `(115, RQ35, CUIT, DateFrom)`
   - Si no existe → INSERT con `LineId = MAX(115.LineId)+1`
3. Para `RQ37` con `AbsEntry=116`:
   - Verifica si ya existe `(116, RQ37, CUIT, DateFrom)`
   - Si no existe → INSERT con `LineId = MAX(116.LineId)+1`

**Resultado**: Se crean **múltiples registros** en WTD3 para el mismo CUIT y período, uno por cada código de retención configurado. Cada uno va a un `AbsEntry` diferente y con su propio `WTCode`.

### Sub-caso 3a: Falla un código pero otros no

Si la inserción de `RQ35` (AbsEntry=115) funciona pero `RQ37` (AbsEntry=116) falla:
1. `RQ35` se inserta en WTD3
2. `RQ37` lanza excepción con `"Error código RQ37: ..."`
3. Se captura la excepción, el `PadronRecord` se actualiza a estado `40` con la nota del error
4. **No hay rollback**: el registro de `RQ35` ya insertado en WTD3 **no se deshace**

---

## Caso 4: Mismo CUIT en diferentes períodos

**Escenario**: El mismo contribuyente aparece en dos importaciones distintas con diferentes `DateFrom`.

**Flujo**:
1. Período Q1 (DateFrom = 2025-01-01):
   - INSERT en WTD3 con `(115, RQ35, CUIT, 2025-01-01)`
2. Período Q2 (DateFrom = 2025-04-01):
   - El check busca `(115, RQ35, CUIT, 2025-04-01)`
   - No existe (DateFrom es diferente) → INSERT nuevo

**Resultado**: Se crean registros separados para cada período. No hay conflicto porque `DateFrom` forma parte de la verificación de unicidad.

---

## Caso 5: Reprocesamiento de errores

**Escenario**: Un `PadronRecord` quedó en estado `40` por error. El usuario hace clic en "Reprocesar Errores".

**Flujo**:
1. `ResetErrorRecordsAsync` cambia el estado de `40` a `10`
2. `ProcessRecordsAsync` se ejecuta nuevamente para ese período
3. Para cada código de retención:
   - Si ya existe en WTD3 (`AbsEntry + WTCode + CUIT + DateFrom`) → skip (caso 2)
   - Si no existe → INSERT normal (caso 1)

**Resultado**: Los códigos que ya estaban insertados correctamente se saltan. Sólo se intentan insertar los que faltan o fallaron.

---

## Resumen de Comportamiento

| Situación | ¿Inserta? | ¿Actualiza? | ¿Error? |
|---|---|---|---|
| No existe registro previo | Sí | No | No |
| Ya existe exactamente igual | No (skip) | No | No (se considera OK) |
| Múltiples códigos configurados | Uno por cada código | No | Depende de cada insert |
| Falla un insert entre varios | Parcial (anteriores quedan) | No | Sí, estado `40` |
| Mismo CUIT, otro período | Sí (DateFrom distinto) | No | No |
| Reprocesamiento | Sólo los que faltan | No | Depende |

## Nota sobre `PR_WTD3`

En el archivo `Readme.md` del proyecto existe un stored procedure `PR_WTD3` (HANA) que sí incluye lógica de **UPDATE**:

```sql
if HAY1 > 0 then
    UPDATE WTD3 SET "DateTo" = TO_DATE(HFEC)
    WHERE "AbsEntry"= :AENTRY AND "KeyPart1"= :CUIT AND "DetailType"= :TIPO;
END IF;
```

Sin embargo, **el add-on actual no utiliza este procedimiento**. El código en `PSaltaRepository.InsertWtd3Direct` solo realiza `INSERT` o `skip`, nunca `UPDATE`. Si se requiere modificar `DateTo` de registros existentes, habría que cambiar la lógica para invocar `PR_WTD3` o implementar un `UPDATE` equivalente.

## Limitaciones Identificadas

1. **Sin actualización**: Si un registro ya existe en WTD3, nunca se actualiza `DateTo`. Esto puede dejar fechas de fin desactualizadas si el período cambia.
2. **Sin rollback parcial**: Si falla el segundo código de retención de una lista, el primero ya quedó insertado en WTD3 pero el `PadronRecord` queda como error (`40`).
3. **LineId por AbsEntry**: El `LineId` se calcula como `MAX(LineId)+1` global para cada `AbsEntry`. Si hay inserts concurrentes o manuales en SAP, podría haber colisiones.
4. **Dependencia de U_Codigo como int**: Si `U_Codigo` no es un número válido, se usa `1` por defecto.
