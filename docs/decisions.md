# Decisiones

Cada entrada dice qué elegí, qué descarté y por qué.

## 2026-11-02 · Emitir con un solo UPDATE ... OUTPUT

**Elegí:** un `UPDATE NcfRanges SET Next = Next + 1 OUTPUT inserted.Next - 1 ... WHERE Next <= Last AND ExpiresOn >= hoy`. SQL Server bloquea la fila, suma uno y devuelve el valor anterior en la misma sentencia.

**Descarté:**
- Leer el rango, calcular y escribir dentro de una transacción serializable, que es lo que hice en AgendaBot para las citas. Funciona, pero aquí la fila es un contador, no un intervalo de tiempo, y una transacción con reintentos es más maquinaria de la que hace falta para sumar uno.
- Un `SEQUENCE` de SQL Server. No entiende de un rango autorizado con tope y vencimiento, y puede dejar huecos si una transacción se revierte.

**Cómo lo comprobé:** el test lanza 40 emisiones a la vez sobre un rango de 40 y espera los números del 1 al 40 sin repetir. Para ver que el test puede fallar sustituí el UPDATE por leer y luego escribir: salió `[1, 1, 1, 1, 1, ...]`.

## 2026-11-02 · Serializable solo al registrar rangos

Dos rangos del mismo tipo no pueden solaparse. La comprobación y el insert van en una transacción serializable para que dos altas simultáneas no se crucen. Con 20 altas del mismo rango queda una sola. Si bajo el aislamiento a `ReadCommitted`, quedan las 20.

Bajo serializable SQL Server aborta a una de las transacciones con el error 1205 cuando se bloquean entre sí. Mi primer reintento solo miraba una capa de `InnerException`, pero EF envuelve la excepción en `InvalidOperationException` y `DbUpdateException`, así que nunca reintentaba y el test fallaba siempre al correrlo solo. Ahora recorre toda la cadena y hace hasta 8 intentos con una espera corta.

## 2026-11-02 · Validar solo la forma

No consulto a la DGII. Depende de un servicio y un padrón que no controlo, y un test que llama a un tercero se rompe por razones ajenas al código. A cambio, la API dice "este RNC es coherente", nunca "esta empresa existe".

## 2026-11-02 · Cuerpo en ceros y municipio 000

`000000002` cumple el módulo 11 y `00000000000` cumple Luhn, pero ninguno existe. Los rechazo con un motivo propio (`zeros`) en vez de dejar que el algoritmo los apruebe.

El RNC se prueba con vectores calculados a mano (la suma ponderada está escrita en el test) y con una propiedad: para cada cuerpo hay exactamente un dígito verificador válido y nunca es cero. La cédula se compara contra una implementación independiente de Luhn en 20 000 números.

## 2026-11-02 · Fecha dominicana como UTC-4 fijo

El vencimiento de un rango se compara con la fecha de República Dominicana. Uso UTC-4 fijo en lugar de la zona `America/Santo_Domingo` porque no hay horario de verano y así la imagen de Docker no depende de tzdata. El último día de vigencia todavía se puede emitir; hay un test para eso.

## 2026-11-02 · Clave de API y límite por IP, sin cuentas de usuario

Es un servicio interno que otro sistema llama, así que una clave compartida en `X-Api-Key` alcanza, comparada en tiempo constante. En producción no arranca sin clave. Los validadores son públicos y no tocan la base, pero todo pasa por un límite de 120 peticiones por minuto por IP. Si algún día lo consumen varios clientes, habría que pasar a una clave por cliente.

## 2026-11-02 · X-Forwarded-For solo con un interruptor

Detrás de un proxy todas las peticiones traen la IP del proxy y el límite por IP sería uno solo para todos los clientes. `Proxy:Trusted=true` activa `UseForwardedHeaders`. Queda apagado por defecto: si el contenedor se pudiera alcanzar sin pasar por el proxy, cualquiera cambiaría `X-Forwarded-For` en cada petición y evadiría el límite. Hay un test para cada caso.

## 2026-11-02 · SQL Server real en los tests

InMemory y SQLite no reproducen los bloqueos de fila ni el aislamiento serializable, que es justo lo que hay que probar. En CI y en local se levanta un contenedor con Testcontainers.

## 2026-11-02 · Migrar al arrancar

Es una sola instancia, así que `Database.Migrate()` al iniciar es lo más simple. Con varias réplicas habría que sacarlo a un paso del despliegue.

## 2026-11-02 · System.Security.Cryptography.Pkcs fijado en 10.0.13

Llega como dependencia transitiva de `Microsoft.Data.SqlClient`. La 9.0.11 y la 10.0.12 tienen avisos de seguridad de NuGet (NU1902 y NU1903), y el build usa `-warnaserror`. La 10.0.13 ya no los tiene. Cuando SqlClient traiga una versión limpia, se puede quitar la referencia explícita.
