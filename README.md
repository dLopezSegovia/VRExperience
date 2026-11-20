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
4. **Sin visor (solo para probar la lógica):** pulsa Play y usa el teclado (ver *Controles*). El
   `OVRHeadsetEmulator` del rig permite mover la cabeza con Ctrl + ratón.

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
| Acción | Mando | Teclado |
|---|---|---|
| Caminar (1,4 m/s, relativo a la mirada) | Stick izquierdo | WASD |
| Girar (por saltos de 30°; se puede cambiar a giro suave en `SmoothLocomotion`) | Stick derecho | Q / E |
| Agarrar / soltar (basura, silla, cubos, linterna) | Grip (agarre ISDK) | — |
| Encender / apagar la linterna | Gatillo o A/X mientras la sostienes | F |
| Rellenar el hoyo | Inclina un cubo con arena sobre el hoyo; para recargarlo, hunde el cubo en la arena | — |
| Terminar la fase | Mantén **B / Y** 1,6 s | Mantén Enter |

### Tortuga (VS2)
| Acción | Mando | Teclado |
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
