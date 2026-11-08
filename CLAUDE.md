# Multijugador LICENE

Proyecto de Unity (6000.0.31f1). El dueño, Oscar, trabaja en este proyecto desde dos
computadoras (PC de casa y laptop), sincronizadas con el repositorio
`OscarMadrazo/LICENE---Multijugador` en GitHub. Responder en español.

## Sincronización (obligatorio)

- **Al empezar cada sesión**, antes de modificar nada: `git pull`. Si hay cambios locales sin
  guardar, avisar a Oscar antes de hacer pull.
- **Al terminar un cambio** (o cuando Oscar diga "guarda" o "sube"): `git add -A`, hacer commit
  con un mensaje en español que explique qué cambió y luego `git push`.
- Si `git push` es rechazado porque hay cambios nuevos en GitHub: `git pull` y después `git push`.
  Si hay conflictos en escenas (`.unity`) o prefabs (`.prefab`), **no resolverlos a ciegas**:
  explicar a Oscar qué archivos chocan y preguntar qué versión conservar.
- Antes de hacer `git pull`, recordar a Oscar que guarde la escena en Unity.

## Reglas del repositorio

- No subir `Library/`, `Temp/`, `Logs/`, `obj/`, `UserSettings/` ni builds (`.apk`, `.exe`, `.zip`,
  carpetas de pruebas). Ya están en `.gitignore`.
- Los archivos binarios pesados (modelos, texturas, audio, video, plugins) van por Git LFS según
  `.gitattributes`. Si se agrega un tipo nuevo de archivo pesado, agregarlo a `.gitattributes`.
  GitHub rechaza archivos de más de 100 MB que no estén en LFS.
- Mantener los `.meta` junto con su archivo (mover, renombrar o borrar los dos juntos).
- Los scripts propios están en `Assets/Scripts/` (cuando exista esa carpeta). No modificar los
  assets de terceros (Asset Store, samples) salvo que Oscar lo pida.
