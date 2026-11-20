# RhythmWare — código para evaluación académica

Repositorio **privado** del proyecto Unity. La descarga para jugadores está en [BlackModerm10/RhythmWare](https://github.com/BlackModerm10/RhythmWare).

## Abrir el proyecto

1. Instala **Unity Hub** y **Unity 6000.5.7f1**, con el módulo Windows Build Support.
2. Clona o descarga este repositorio completo. La raíz contiene `Assets`, `Packages` y `ProjectSettings`.
3. Añade esa carpeta como proyecto existente en Unity Hub. El editor importará los recursos y resolverá las dependencias fijadas en `Packages/manifest.json` y `packages-lock.json`.
4. Abre **Assets/Scenes/SampleScene.unity**; una escena nueva vacía no muestra el juego.
5. Pulsa **Play**, crea un alias y calibra el audio.

Se utilizan URP, TextMesh Pro y el Input System. Los recursos y sus `.meta` están incluidos. Unity regenera `Library`, los temporales y los proyectos del IDE.

## Compilar para Windows

Utiliza **RhythmWare → Build → Windows listo para compartir**. El proceso incorpora icono, música, efectos y campaña. Distribuye la carpeta generada completa, con `RhythmWare.exe`, `RhythmWare_Data` y las dependencias.

La beta de la experiencia está disponible en [Releases del repositorio público](https://github.com/BlackModerm10/RhythmWare/releases/tag/v0.2.0-beta.3).

## Conexión online

**Assets/Resources/OnlineSettings.json viene desactivado y sin URL ni clave.** Esta copia funciona con perfiles y registros locales. No se incluye la configuración del servicio utilizado por los participantes.

Para reproducir la integración en tu propia instancia, sigue [Backend/README.md](Backend/README.md). El cliente utiliza únicamente una clave publicable; las claves administrativas no deben incorporarse al juego ni a Git.

## Organización

| Carpeta | Contenido |
|---|---|
| `Assets/Scripts/Core` | Estado, audio, perfiles, configuración y sincronización |
| `Assets/Scripts/Rhythm` | Notas, carriles, juicios, mapas y editor |
| `Assets/Scripts/Education` | Contenido y progreso de aprendizaje |
| `Assets/Scripts/UI` | Menús y presentación |
| `Assets/Scripts/Editor` | Exportación y validadores |
| `Assets/Resources` | Campaña, escenarios, enemigos y configuración de ejemplo |
| `Assets/Audio` | Audio necesario para compilar |
| `Backend` | Esquema, funciones y pruebas del servicio |

## Verificación

- `RhythmWare.EditorTools.VisualRedesignValidation.RunParticipants` valida perfiles y regresiones. Ejecútalo en una **copia de pruebas**: abre la escena, cambia vistas y genera capturas y reportes.
- Instala las dependencias SQL con `npm ci --prefix Backend/tests` y ejecuta `npm test --prefix Backend/tests`. Utilizan PostgreSQL/WebAssembly y datos ficticios.
- El diagnóstico optativo del ejecutable se inicia mediante `--rw-self-test <ruta-del-informe>`; no forma parte del flujo normal del jugador.

No se incluyen perfiles, resultados experimentales, códigos de recuperación ni archivos internos de la investigación. Los datos experimentales se analizan y entregan por separado.

## Experiencia de evaluación

El [README público](https://github.com/BlackModerm10/RhythmWare) contiene las instrucciones para jugadores. El [pretest](https://forms.gle/tWyXDhHbhvTkP41b9) se completa antes de abrir el juego y el [postest](https://forms.gle/tZ7bom8BJFBErc5u6) después de la sesión. Los formularios y las respuestas SOC internas son fuentes distintas.

## Acceso del profesor

Conserva este repositorio privado. Invita a la cuenta de GitHub del profesor desde la configuración de colaboradores: enviar únicamente el enlace no concede acceso. Las personas autorizadas podrán descargar el código para evaluarlo.

[Créditos](CREDITOS.md).
