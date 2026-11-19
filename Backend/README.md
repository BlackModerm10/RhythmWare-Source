# Activar los perfiles y la clasificación global de RYTHMWARE

Esta copia conserva el diseño del backend para evaluación académica. No incluye la configuración ni los datos del servicio de la experiencia. OnlineSettings.json se entrega desactivado; utiliza una instancia independiente para reproducir la integración.

## Repetir la instalación en otro proyecto

1. Crea un proyecto en tu cuenta de [Supabase](https://supabase.com/dashboard). Solo el responsable necesita esta cuenta; los jugadores no introducen correo ni contraseña.
2. En Authentication, habilita **Anonymous Sign-Ins**. La guía oficial enlaza la configuración vigente: https://supabase.com/docs/guides/auth/auth-anonymous. El cliente usa `/auth/v1/signup` y refresh tokens; configurar CAPTCHA requiere integrar también su comprobación en el cliente. Revisa la cuota de altas anónimas si probarán muchos jugadores desde una misma red.
3. Desde SQL Editor ejecuta, en orden:
   - `supabase/migrations/202611150001_participants.sql`
   - `supabase/migrations/202611150002_campaign_and_questions.sql`
   - `supabase/migrations/202611150003_auth_link_index.sql`
4. Mantén `rw_private` fuera de los esquemas expuestos por Data API. Las tablas privadas tienen RLS sin acceso directo para `anon` o `authenticated`; se escriben únicamente mediante funciones acotadas a la identidad autenticada. La tabla pública contiene solo alias, puntuación, precisión, combo, rango, mapa y revisión. La asociación con el participante está en el esquema privado.
5. Copia la **Project URL** y la **Publishable key** del proyecto. La clave legacy `anon` también se admite. No uses `sb_secret_…`, `service_role`, contraseña de base de datos ni claves administrativas en el juego.
6. Edita `Assets/Resources/OnlineSettings.json` en Unity:

```json
{
  "enabled": true,
  "projectUrl": "https://TU_PROYECTO.supabase.co",
  "publishableKey": "sb_publishable_TU_CLAVE_PUBLICA"
}
```

7. Genera una nueva compilación Windows desde **RhythmWare → Build → Windows listo para compartir**. Un ejecutable anterior no recibe por sí solo los cambios de este archivo.
8. Prueba dos perfiles en dos instalaciones: ambos deben ver las mejores partidas; las respuestas SOC e intentos completos se consultan desde tu Dashboard, no desde la API pública. Conserva una copia de seguridad de la base de datos durante el piloto.

## Qué hace el jugador

Primer inicio: elige alias → crea perfil → calibra audio → juega. Los siguientes inicios recuerdan el participante y su acceso. Ajustes → Perfil permite crear/seleccionar otro participante, cambiar el alias, mostrar/copiar el código privado y recuperar una identidad.

Los alias pueden repetirse; una etiqueta de cuatro cifras los distingue en el servidor. La identidad interna permanece estable si cambia el alias o se recupera desde otro dispositivo. Un código `RW-…` tiene 256 bits aleatorios y el servidor almacena únicamente su hash. Es un secreto de acceso: conocer el alias no permite recuperar una cuenta. El código actual sigue siendo válido al recuperar el perfil, de modo que no se pierde el acceso por una respuesta de red interrumpida.

La sesión y el código se protegen mediante DPAPI de Windows; no aparecen en los JSON de perfil, en la leaderboard ni en los registros de partidas. Esta implementación está dirigida a Windows. El selector local no constituye una separación entre personas que comparten la misma sesión de Windows; la privacidad del servidor impide consultar el historial de otros jugadores.

El código funciona en otro PC después de sincronizar el perfil por primera vez. Recuperar restaura la identidad, los récords globales y las primeras/últimas respuestas SOC sincronizadas. Los archivos que nunca salieron del PC original no pueden recuperarse desde el servidor. No se pide correo ni se crea una contraseña de uso diario.

## Datos y desconexiones

- Público: una mejor partida terminada por participante, mapa y revisión; alias, posición, puntuación, precisión, combo y rango.
- Privado: todos los intentos de los tres mapas oficiales, con victoria/derrota/abandono/interrupción; estadísticas, fechas, sesión, versión y ajustes. Las demostraciones protegidas del tutorial y las pruebas del editor no se registran como partidas de campaña.
- Privado: respuestas SOC con contexto (`academy`, `tutorial-rescue`, `rescue`, `defeat`, `debrief`), corrección calculada por el servidor y exposición local previa. No mezclar estos contextos para interpretar aprendizaje; no sustituyen un pretest/postest controlado.
- Una cola por participante conserva los eventos sin conexión. Cada evento tiene un UUID y el servidor ignora los reenvíos ya recibidos. Cambiar de participante no reasigna los eventos pendientes.
- Los registros rechazados por validación se conservan en `retained`, sin impedir los envíos válidos posteriores. «Reintentar conexión» los vuelve a poner en cola tras corregir la causa; el archivo de historial completo se conserva.
- Se guarda un punto de recuperación por intento. Tras un cierre inesperado se registra como interrumpido; `has_final_stats=false` indica que las cifras corresponden al último punto guardado, no necesariamente al instante exacto del cierre.

Archivos por participante: `%USERPROFILE%\AppData\LocalLow\DefaultCompany\Rythmware\Participants\<id-local>`. Incluyen `scores-v1.json`, `soc-learning-v2.json`, `game-attempts-v1.jsonl`, la cola `outbox` y el acceso cifrado. El primer perfil recibe una **copia** del historial antiguo; los originales permanecen en la carpeta raíz. Ese historial previo no se sube retroactivamente: se envían los eventos nuevos del perfil. Los ajustes de audio, offset, velocidad, calibración y teclas se separan por participante.

## Consultar y exportar tus datos privados

Desde SQL Editor, con tu cuenta de responsable, abre `supabase/admin-export.sql`. Ejecuta cada consulta y descarga su CSV: historial de partidas, respuestas SOC y resumen por participante. El mismo `participant_code` vincula las tres consultas, incluso si el jugador cambia de alias o dispositivo. No se publican estos resultados en el juego.

No entregues credenciales de Dashboard ni claves administrativas a los jugadores. El juego solo incluye la clave pública; el control real de acceso está en SQL/RLS y en las funciones del servidor, no en esconder botones.

## Actualizaciones y comprobaciones

Los mapas se identifican por ID y SHA-256 del recurso original. Al cambiar un mapa, ejecuta `node Backend/generate-seed.cjs` desde la raíz del proyecto y aplica el nuevo seed antes de distribuirlo. Se conservan las definiciones anteriores: cada revisión tiene su clasificación independiente. Los personalizados y los charts oficiales modificados no se envían como campaña oficial.

Las funciones validan límites de puntuación, duración, juicios, ajustes y contenido SOC; no constituyen un sistema antitrampas que verifique las pulsaciones reales. El servidor no debe tratar los tiempos del reloj del jugador como una fuente independiente de medición.

Se incluyen pruebas SQL reales sobre PostgreSQL en WebAssembly (`Backend/tests/privacy.cjs`; instala sus dependencias con `npm ci --prefix Backend/tests` y ejecuta `npm test --prefix Backend/tests`), además del validador Unity `RhythmWare.EditorTools.VisualRedesignValidation.RunParticipants`. Las pruebas usan identidades ficticias y carpetas aisladas. El despliegue activo y el transporte real de Unity pasaron 89 comprobaciones, incluyendo aislamiento entre participantes, recuperación y reenvío sin duplicados. Las identidades ficticias se retiraron del servidor después de la prueba.

Referencias oficiales: [autenticación anónima](https://supabase.com/docs/guides/auth/auth-anonymous), [claves públicas y secretas](https://supabase.com/docs/guides/getting-started/api-keys), [RLS](https://supabase.com/docs/guides/database/postgres/row-level-security), [funciones y permisos](https://supabase.com/docs/guides/database/functions).
