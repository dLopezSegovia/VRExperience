# Same Shore: One Beach, Two Bodies

Experiencia de realidad virtual para el máster CSIM (UPF). Primero recorres una playa de noche como
**persona adulta (VS1)**. Después recorres **la misma playa** como **cría de tortuga marina (VS2)**, que
tiene que salir del nido y llegar al mar. Lo que hagas como humano (recoger basura, apagar la linterna,
rellenar el hoyo, mover la silla) es exactamente lo que la tortuga encuentra después.

- **Unity** 6000.6.4f1 · **URP** 17.6 · **Meta XR SDK v207** (OVRCameraRig + Interaction SDK) sobre OpenXR
- Dispositivo de referencia: **Meta Quest 3** (build Android, o PCVR con Meta Quest Link)
- Escena principal: **`Assets/SameShore/Scenes/SameShore.unity`** (índice 0 en Build Settings)

---

## Cómo abrir y ejecutar

1. Abre la carpeta `VRExperience` con Unity 6000.6.4f1.
2. Abre `Assets/SameShore/Scenes/SameShore.unity`.
3. **En el visor (recomendado):** conecta las Quest 3 con Quest Link / Air Link y pulsa Play.
   Para un build autónomo, usa el perfil *Meta Quest* (Android) y *Build And Run*.
4. **Sin visor:** pulsa Play en el Editor. El **modo Desktop Testing** se activa solo (ver abajo).

## Rediseño narrativo (rama actual)

- **Acto I, humano:**
  - Intro en negro con subtítulos, y la playa aparece poco a poco.
  - Objetivos en un Canvas World Space arriba a la izquierda (`ObjectiveTracker` / `ObjectiveHUD`), con un marcador espacial y una flecha hacia el objetivo actual.
  - Objetivos: linterna, plástico, hoyo, silla y nido.
  - Se pueden dejar objetivos sin hacer: quedan marcados como "left behind".
  - Cuerpo de tres puntos con IK (`HumanAvatar`).
  - La linterna se sostiene orientada como en la mano (`GripAlignment`) y su cono de luz es visible.
  - Silla con física mejorada y una zona segura señalizada.
- **Nido:** se oye la arena moverse y sale una cría. Aparece una elección: "Go back" o "Become the hatchling".
- **Acto II, tortuga:**
  - Transición con respiración, fundido y el texto "One beach. A completely different world.". La cría sale de la arena y "abre los ojos".
  - Objetivos de la tortuga en "YOUR JOURNEY".
  - Rastro de aletas, nubes de arena, piedrecitas a escala y un zumbido mientras tira la luz artificial.
  - Gaviota rediseñada (`GullHunt`): sombra, círculo de aviso y picado. Se esquiva saliendo del círculo o a cubierto. Si te atrapa dos veces, vuelves al punto de control.
- **Final (`FinalWave`):** una ola cubre la cámara y pasa a un ambiente submarino. Después, "Great Job" con las consecuencias de lo que hizo el humano y "Same shore. Different lives. Your actions matter.".
- **Menú de pausa:** botón Menú en el mando, o Esc/P en escritorio. Permite reiniciar la fase o volver al último punto de control.
- **Pruebas en Play Mode con entrada simulada** (`Tools/Unity/run_batch.ps1 -Mode clean|dirty|xr`). Resultados de la última ejecución:
  - `clean`: 34/34.
  - `dirty`: 13/13 (antes del último ajuste del montículo y de la prueba).
  - `xr`: 7/7.
- **Recursos nuevos:**
  - Modelos con `Tools/Blender/make_assets_v2.py`.
  - Sonidos con `Tools/Audio/synth_audio_v2.py`.
  - Son propios y no hay recursos de terceros nuevos.

## Modo Desktop Testing (Editor sin Meta Quest)

Se activa automáticamente al pulsar Play en el Editor si no hay ningún dispositivo XR funcionando. Con
las Quest 3 (build Android o Quest Link) no se activa y los controles XR no cambian.

- Puedes forzarlo en el componente `DesktopTestingMode` (`SameShore_Systems`): *Auto / ForceDesktop / ForceXR*.
- También con la variable de entorno `SAMESHORE_DESKTOP=1/0`, o con `-sameshore-desktop` / `-sameshore-xr`.

Cómo funciona, reutilizando lo existente:
- **Cámara:** usa el mismo `OVRCameraRig`. `TrackingSpace` se eleva a 1,65 m y gira con el ratón, que es
  lo que haría el visor. Como tortuga, la escala 0,05 deja los ojos a ~8 cm igual que en VR.
  `OVRHeadsetEmulator` se desactiva solo en este modo.
- **Menús:** se usan los mismos botones del menú y de las pantallas finales.
  - Busca el `EventSystem` de la escena y, si no hay ninguno, lo crea.
  - Le añade `InputSystemUIInputModule` (el proyecto usa solo el Input System) y desactiva los módulos
    incompatibles.
  - Pone un `PhysicsRaycaster`, limitado a la capa `UI`, en la cámara.
  - `UIButton` recibe hover y clic del ratón por el EventSystem; el láser de los mandos sigue igual.
- **Interacción:** `DesktopInteractor` actúa sobre los mismos `Grabbable`, `Flashlight`, `SandBucket`,
  `TrashBin` y `GrabFeedback` que el agarre XR.

| Acción | Humano | Tortuga |
|---|---|---|
| Moverse | WASD | W / S reptar · A / D girar el cuerpo |
| Mirar | Ratón | Ratón |
| Coger / soltar (basura, cubo, linterna, silla) | Clic izq. o **E** | — |
| Usar: encender/apagar la linterna (sostenida o apuntada), volcar el cubo (mantener) | Clic der. o **F** | — |
| Recargar el cubo | Con el cubo cogido, apunta a la arena | — |
| Tirar a la papelera | Apunta a la papelera con el objeto y suéltalo | — |
| Impulso | — | Espacio |
| Terminar la noche | Mantén Enter | — |
| Menús y pantallas finales | Ratón (hover + clic) o 1 / 2 / 3, R / M | igual |
| Liberar / capturar el cursor | Esc / clic en la vista | igual |

Abajo aparecen una mira y una línea de ayuda (solo en este modo).

Si cambias `Assets/Scenes/SampleScene.unity` o los scripts del constructor, regenera la escena con
**menú `Same Shore → Build Scene`** y compruébala con **`Same Shore → Validate Scene`**.
La escena `SampleScene` del equipo no se modifica: el constructor la copia y trabaja sobre la copia.

## Cómo probar cada experiencia

Al empezar aparece el menú inicial (apunta con un mando y pulsa el gatillo; con teclado, 1/2/3):

| Opción | Qué hace |
|---|---|
| **Full Experience** | Humano → transición (resumen de lo que has cambiado + "Tonight, the nest hatches.") → tortuga → mar |
| **Human Experience** | Solo la fase humana. Al terminar, puedes *Continue as a hatchling* (con la playa tal como la dejaste) o volver al menú |
| **Turtle Experience** | Empieza en el nido con la playa en su estado inicial (linterna encendida, basura y hoyo abierto) |

Final de la tortuga: al llegar a la zona que mojan las olas aparece **"Great Job"** con estadísticas, y
puedes repetir (*Crawl again*, la playa se conserva) o volver al menú (recarga la escena desde cero).
Si las gaviotas te atrapan 3 veces, aparece una pantalla de derrota con *Try again*.

## Controles

### Humano (VS1)
| Acción | Mando (Quest 3) | Desktop Testing |
|---|---|---|
| Caminar (1,4 m/s, relativo a la mirada) | Stick izquierdo | WASD |
| Girar (por saltos de 30°; se puede cambiar a giro suave en `SmoothLocomotion`) | Stick derecho | Ratón (Desktop Testing) |
| Agarrar / soltar (basura, silla, cubos, linterna) | Grip (agarre ISDK) | — |
| Encender / apagar la linterna | Gatillo o A/X mientras la sostienes | F o clic der. |
| Rellenar el hoyo | Inclina un cubo con arena sobre el hoyo; para recargarlo, hunde el cubo en la arena | — |
| Terminar la fase | Mantén **B / Y** 1,6 s | Mantén Enter |

### Tortuga (VS2)
| Acción | Mando (Quest 3) | Desktop Testing |
|---|---|---|
| Reptar (brazadas a pulsos mientras mantienes el stick; sin desplazamiento lateral) | Stick (cualquiera) adelante | W |
| Reptar marcha atrás (más lento) | Stick atrás | S |
| Girar el cuerpo despacio (25°/s) | Stick izquierda/derecha | A / D |
| Impulso súbito (se recarga en 2,5 s) | A / X | Espacio |
| Saltar textos de la transición | Gatillo | Espacio |

## Qué se ha implementado

**Flujo** (`ExperienceDirector`): menú → humano → transición → tortuga → final. Usa los estados del
`GameManager` del equipo (Intro, Human, Transition, Hatchling, End). Cada reubicación del jugador ocurre
con la pantalla en negro (`ScreenFader`), así que la cámara nunca salta a la vista.

**Persistencia causal**: las dos fases ocurren en la **misma escena**. No hay que copiar nada: cada botella
movida, el estado de la linterna, el hoyo rellenado, la silla o las **huellas** que deja el humano al caminar
siguen ahí para la tortuga. `BeachStateTracker` solo lo resume en la transición.

**Humano**: `SmoothLocomotion` (del equipo, ampliado: giro por saltos, teclado, colocación),
`HumanController` (pisadas con sonido y huellas reales en la arena, terminar con B/Y; desactiva el
teletransporte ISDK para que el stick solo camine), agarre con el Interaction SDK de Meta (la misma
configuración que usaba el equipo en la linterna, la silla y los cubos, replicada en la basura nueva),
papelera (`TrashBin`), linterna (`Flashlight` del equipo), cubos de arena (`SandBucket`) y hoyo real
excavado en el terreno (`SandHole`; el terreno se clona en tiempo de ejecución y el asset no se toca).
Barrera invisible en la orilla (`HumanSeaBarrier`): el humano no puede entrar en el agua.

**Tortuga**: `TurtleController` (del equipo, ampliado):
- Brazadas a pulsos, marcha atrás, impulso súbito y giro de 25°/s.
- Atracción por la luz: la suma ponderada de todas las fuentes (`LightSource.GetNetPull`). El brillo
  natural del horizonte marino orienta hacia el mar; la linterna (si sigue encendida y apunta al camino)
  y el resplandor del hotel la desvían. La atracción desvía cada brazada y gira el cuerpo poco a poco
  mientras repta, sin quitar el control al jugador.
- Zonas: el hoyo atrapa (con escape garantizado tras unos segundos de esfuerzo), la bolsa enreda
  ("A jellyfish? Food! …No. It's a plastic bag."), las huellas frenan y la orilla es el objetivo.
- Detección de bloqueo con aviso.
- Empuje de objetos ligeros; los pesados (silla, rocas) obligan a rodearlos.

`HatchlingAvatar` anima las aletas en cada brazada; las crías hermanas (`HatchlingSibling`) salen del nido
cuando el humano se acerca y, en la fase tortuga, reptan con la misma atracción por la luz.

**Percepción** (`PerceptionController`):
- Humano: visión apagada y desaturada (Volume URP).
- Tortuga: tonos añadidos, desplazados hacia el violeta.
- **Foco de atención de ±45° alrededor del cuerpo** (`TurtleVision.shader`): la periferia se oscurece. El
  seguimiento de cabeza sigue siendo 1:1 y nunca se bloquea; además actúa como viñeta de confort.
- Halo cálido en la dirección hacia la que tira la luz.
- Planos de recorte y niebla adaptados a la escala.

**Escala 20×**: el rig del jugador se escala a **0,05** y se cuelga de `TurtleBody` (enfoque del equipo).
Los ojos quedan a ~8 cm (1,65 m × 0,05) y la distancia interpupilar también se reduce 20×, así que el mundo
se *percibe* 20 veces más grande sin escalar la escena ni romper las físicas. El cuerpo físico de la tortuga
es un CharacterController de 3,5 cm de radio, y el plano cercano de la cámara baja a 3 mm.

**Gaviotas** (`Seagull`, `SeagullFlock`): con el humano descansan en la orilla y huyen si te acercas. Con la
tortuga, dos sobrevuelan y se lanzan en picado, anunciado con un graznido y un latido. Para esquivarlas,
haz un impulso o refúgiate bajo la silla. Cada golpe aturde 1,6 s; al tercero, te atrapan.

**Escenario**:
- La cala del equipo (terreno intacto), con la luna creciente baja sobre el mar y su luz fría.
- Océano animado propio con brillo lunar y espuma, y una franja de oleaje (*swash*) que sube y baja por la
  arena dejando arena mojada.
- Nido vallado con cuerda, cartel y luz ámbar "segura para tortugas" (no atrae a las crías).
- Basura: botellas PET y de vidrio, bolsas, latas y un envase.
- Rocas, madera de deriva, conchas, hierba de duna, castillo de arena, papelera, gaviotas, rastros de
  huellas y hotel lejano con ventanas iluminadas.

**Sonido** (`AmbientAudio`, fuentes 3D):
- Humano: olas, viento, música lejana del hotel y pisadas.
- Tortuga: oleaje más cercano y potente, música y voces humanas amortiguadas (filtro paso bajo), aletas y
  arrastre de arena, gaviotas y latido.

## Estructura

```
Assets/SameShore/
  Scenes/SameShore.unity        escena principal (generada desde SampleScene)
  Scripts/Core                  director, fundido, percepción, estado de la playa, entrada XR
  Scripts/Human                 controlador humano, huellas
  Scripts/Interaction           basura, papelera, cubo, hoyo, feedback de agarre
  Scripts/Turtle                avatar, crías hermanas, nido
  Scripts/Environment           gaviotas, audio ambiental
  Scripts/UI                    paneles VR, botones, puntero láser
  Shaders/                      océano, orilla, visión de la cría, fundido, UI, cielo
  Editor/                       constructor de escena, validador, capturas
  Art/, Audio/, Generated/      recursos (ver CREDITS.md)
Assets/Scripts/                 scripts del equipo (conservados y ampliados)
Tools/Blender, Tools/Audio      scripts que generan modelos y sonidos
```

Los valores importantes son configurables en el Inspector: velocidades, brazadas, impulso, atracción de la
luz, ángulo de atención, gaviotas, volúmenes por fase y tiempos de transición.

## Validación realizada (sin visor)

Todo se ha comprobado con Unity en modo batch:

- **Compilación** sin errores; **validador** (`Same Shore → Validate Scene`) sin scripts perdidos ni
  referencias nulas.
- **Capturas renderizadas** de la vista humana y de la vista de la tortuga
  (`SameShorePreview`, `SameShoreShotsOnly`).
- **Prueba automática en Play Mode** (`Same Shore → Run Smoke Test (Play Mode)`; informe en
  `Logs/SameShoreSmokeTest.txt`). Simula la entrada con el teclado y recorre los tres modos:
  - **Full** (35 comprobaciones):
    - menú → humano → transición → tortuga;
    - escala 0,05, ojos a 8 cm y plano cercano a 3 mm;
    - persistencia de la botella en la papelera, la linterna apagada y el hoyo relleno;
    - la luz del mar orienta y la linterna desvía la trayectoria (0,7 m en 4 s);
    - brazadas reales (~0,17 m/s) y giro de 25°/s;
    - llegada al mar con "Great Job" y reintento conservando la playa.
  - **Turtle**: playa intacta; el hoyo retiene a la tortuga y luego la deja salir; una gaviota golpea y
    aturde a una tortuga quieta.
  - **Human**: caminar a 1,40 m/s con huellas; la barrera impide entrar en el mar; el panel final da paso
    a *Continue as a hatchling*, y las huellas persisten.

  - **Desktop** (38 comprobaciones). Usa solo un ratón y un teclado simulados (Input System), sin invocar
    métodos:
    - EventSystem, módulo de entrada y raycaster detectados;
    - hover y clic del ratón en *Full Experience*;
    - captura del cursor, Esc y mirar con el ratón;
    - caminar con WASD, coger una botella con E y tirarla a la papelera con clic;
    - apagar la linterna con F;
    - volcar el cubo con clic derecho, recargarlo en la arena y rellenar el hoyo;
    - mantener Enter, comprobar la persistencia y reptar, girar e impulsarse como tortuga;
    - llegar al mar ("Great Job");
    - hover y clic en *Main menu*, que recarga la escena;
    - hover y clic en *Turtle Experience*.
  - **Ruta XR**: la prueba *Full* también pasa con `SAMESHORE_DESKTOP=0` (modo escritorio apagado).

Para elegir el modo de la prueba, escribe `full`, `human`, `turtle` o `desktop` en
`Logs/SameShoreSmokeMode.txt`.

## Pendiente / requiere prueba real en Quest 3

- **No se ha podido probar con las Quest 3**: la validación se hizo en modo batch (compilación, validador de
  referencias y capturas renderizadas). Hay que comprobar en el visor:
  - el confort de las brazadas de la tortuga (pico de ~0,45 m/s en el mundo, unos 9 m/s percibidos);
  - el agarre ISDK de los objetos nuevos;
  - el umbral del vertido del cubo;
  - la legibilidad de los paneles;
  - el rendimiento en Android: terreno, sombras de la luna y de la linterna, y post-procesado.
- Si el agarre del ISDK de los mandos usa el gatillo, la linterna también se puede encender con A/X.
- No hay soporte específico para seguimiento de manos sin mandos: los menús usan el láser de los mandos.
- Faltan el modelo y la licencia de las texturas `hawksbill.*` (ver CREDITS.md).
