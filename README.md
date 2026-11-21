# Same Shore: One Beach, Two Bodies

Experiencia de realidad virtual para el máster CSIM (UPF).

**Storyboard v2.** Primero vives **un día perfecto de vacaciones** en la playa como persona (Acto I). Después
vives **la misma noche** en el cuerpo de una **tortuga recién nacida** que tiene que llegar al mar (Acto II).
Lo que hiciste para divertirte como humano se convierte, sin que lo pretendieras, en los obstáculos y
peligros de la tortuga.

- **Unity** 6000.6.4f1 · **URP** 17.6 · **Meta XR SDK v207** (OVRCameraRig) sobre OpenXR
- Dispositivo de referencia: **Meta Quest 3** (build Android, o PCVR con Meta Quest Link)
- Escena principal: **`Assets/SameShore/Scenes/SameShore.unity`** (índice 0 en Build Settings)

---

## Cómo abrir y ejecutar

1. Abre la carpeta `VRExperience` con Unity 6000.6.4f1.
2. Abre `Assets/SameShore/Scenes/SameShore.unity`.
3. **En el visor (recomendado):** conecta las Quest 3 con Quest Link / Air Link y pulsa Play.
   Para un build autónomo, usa el perfil *Meta Quest* (Android) y *Build And Run*.
4. **Sin visor:** pulsa Play en el Editor. El **modo Desktop Testing** se activa solo (ver abajo).

La escena se **genera** a partir de `Assets/Scenes/SampleScene.unity` (que no se modifica): menú
**`Same Shore → Build Scene`** y después **`Same Shore → Validate Scene`**.

## Menú inicial

| Opción | Qué hace |
|---|---|
| **Full Experience** | Acto I (el día del humano) → transición → Acto II (la tortuga) → final |
| **Human Experience** | Solo el día del humano. Al terminar: *Continue as the hatchling* (con la playa tal como la dejaste), repetir o menú |
| **Turtle Experience** | Empieza como tortuga con una **playa por defecto** completa: latas en la arena, castillo y hoyo, bolsa en la orilla, guirnalda encendida, música sonando, restos de fuegos y el humano con su linterna |

## Acto I: THE HUMAN, a perfect day at the beach

Objetivos en un panel espacial arriba a la izquierda (**TODAY'S PLANS** / **TONIGHT**). Se marcan solos al
cumplirse y la flecha y el marcador señalan el siguiente.

1. **A day at the beach** (tarde soleada). Empiezas **sentado en la silla de playa**, a la sombra de la sombrilla,
   con toalla, nevera, mesita, dos bebidas, café, latas de refresco, gafas de sol, altavoz y bolsa de playa.
   - *Pick up your drink* (llévatela a la boca y suena un sorbo).
   - *Turn on the music*: botón *play* del altavoz (tócalo con el dedo o apunta y aprieta el gatillo).
2. **Can toss challenge.** Tres latas vacías sobre la nevera y una papelera a unos 4 m. Se lanzan con física
   real (la velocidad de la mano al soltar el grip). Si entra, cuenta. Si falla, **la lata se queda donde cae**:
   no desaparece ni vuelve a su sitio. La línea silla → papelera **cruza el camino de las tortugas**.
   Contador sobre la papelera: `CAN TOSS 1/3 THROWN · 0 IN`.
3. **Build a sandcastle.** Con la pala cavas arena en un punto fijo: cada palada **excava un hoyo real en el
   terreno** (`SandHole`). La echas al cubo (3 paladas), le das la vuelta sobre la marca del castillo, lo sueltas y
   lo levantas: aparece una torre. Con 2 torres el castillo se completa (murallas y bandera). **El hoyo se queda.**
4. **Snack time.** Vuelves a la toalla y sacas el sándwich de la bolsa (3 mordiscos). El envoltorio de plástico
   cae en la toalla y **una ráfaga de viento** lo lleva, con una trayectoria determinista (sin física), hasta la
   arena mojada junto al camino. *"Oh well... it's just a bag."* No hace falta recogerla.
5. **Evening entertainment.** El cielo pasa a atardecer. Subes el volumen y lanzas los **fuegos artificiales**
   con el botón rojo de la zona acordonada junto al agua: nueve carcasas sobre el mar, destellos, reflejo en el
   agua y sonido que llega con el retardo de la distancia. La batería gastada sigue humeando.
6. **Decorate the beach.** Cae la noche (luna y estrellas). Coges el carrete de luces y lo enganchas en la
   sombrilla y en **cuatro postes en zigzag** hasta el nido (toca el gancho que brilla, o gatillo cerca de él).
   Cada tramo es un cable con caída parabólica (sin cuerdas físicas) con bombillas. Al terminar se encienden.
7. **The flashlight and the nest.** Linterna en la toalla. **Agarre corregido:** se sostiene con una pose fija
   y el haz sale en la dirección en la que apunta el mando (la misma del láser de los menús). Gatillo:
   encender/apagar. Sigues las luces hasta el nido: **"Wow... baby turtles!"**, las crías asoman. Vuelves a la toalla.
8. **The photo.** Una cría avanza hacia el mar. Te acercas: **"What a perfect photo!"**. El móvil aparece delante
   de ti; la pantalla muestra la cámara en directo; gatillo = foto con **flash**. Fundido:
   *"For you, it was just another perfect day."* · *"But what was it like for her?"*

## Acto II: THE TURTLE, a journey to survive

*"Earlier that evening, beneath the sand..."* (se sienten los fuegos como retumbos y vibraciones) →
*"The same night. A few minutes before the photo."* La cría sale del nido.

- **Cuerpo y cámara:** ojos a **~8 cm** de la arena (rig a escala 0,05), aletas visibles al reptar.
  Stick adelante = brazadas, atrás = marcha atrás, izquierda/derecha = girar despacio (25°/s). A/X = impulso
  opcional (nada depende de él).
- **Ruta semilineal** del nido al mar, enmarcada por rocas, madera y el desnivel. Objetivo **REACH THE OCEAN**
  con subobjetivos: salir del nido, ir hacia el brillo del mar, pasar las latas gigantes, superar el hoyo,
  vigilar el cielo, ¿comida?, pasar al gigante, llegar al mar.
- **Latas:** las que fallaste, en el mismo sitio, enormes. **Hoyo:** si te acercas te deslizas dentro y sales
  reptando (siempre se puede salir). **Bolsa:** *"You're hungry... Is that a jellyfish?"*: late como una
  medusa; al morderla: *"It's not a jellyfish. It's a plastic bag."*, arcadas y lentitud unos segundos,
  y te recuperas.
- **Ruido humano:** la **misma canción** del altavoz, oída con filtro paso bajo, distorsión, reverberación de
  cueva y tono más grave, y los graves como pulsos suaves en los mandos. El volumen del altavoz nunca pasa
  de 0,85: el efecto viene del filtrado y la espacialización, no de los decibelios.
- **Luces que confunden:** cada tramo de la guirnalda es una fuente de luz que atrae (`LightSource`), igual que
  la linterna del humano. El horizonte marino es la referencia natural. La atracción desvía las brazadas y
  gira el cuerpo poco a poco; la cámara nunca se gira y siempre puedes corregir.
- **Gaviotas:** **dos encuentros como máximo**, nunca mortales. (1) Una sombra cruza la arena y la gaviota pasa.
  (2) Vuelve, da vueltas, se queda suspendida y chilla; un anillo marca dónde atacará: basta con salir del
  anillo reptando (o esconderse junto a algo). Si te alcanza: aturdimiento breve y un empujón.
- **El gigante y la fotografía:** el humano del Acto I **repite lo que hizo**: su paseo grabado desde el nido
  (con la linterna encendida si la llevabas) y, en el **mismo sitio** donde hiciste la foto, se agacha, saca el
  móvil y dispara. Deslumbramiento moderado (velo cálido de ~0,55 que se desvanece en 2,4 s, sin
  estroboscopio) → *"It wasn't a monster. It was the human... it was you, taking the perfect photo."*
- **Final:** *"Almost there..."*, la ola cubre la vista, oscuridad. Parece que lo ha conseguido; en la oscuridad,
  el chillido de una gaviota que se acerca, aleteo, el golpe, silencio. **SAME SHORE** ·
  *"One beach. Two bodies. Two very different worlds."* · *"What seemed harmless to one became a struggle for
  the other."* → *Experience it again* / *Main menu*. La muerte solo ocurre aquí.

## Continuidad entre los dos actos

Los dos actos ocurren en **la misma escena**: todo objeto sigue físicamente donde lo dejó el humano (latas,
hoyo del terreno, bolsa, guirnalda y sus luces, batería de fuegos, música sonando). `BeachLog` guarda además lo
que no se ve después: la cronología de eventos, cada lata (acierto/fallo y dónde quedó), castillo y hoyo,
bolsa, anclajes de la guirnalda, estado del altavoz, fuegos lanzados, linterna, **el recorrido nocturno del
humano** (muestras cada 0,2 s con la linterna) y **la foto** (posición, orientación, móvil, flash).
`TurtleStory` lee ese estado y `GiantHuman` lo reproduce. En *Turtle Experience* se aplica un estado por defecto completo.

## Controles

| Acción | Humano (Quest 3) | Tortuga (Quest 3) | Desktop Testing |
|---|---|---|---|
| Moverse | Stick izq. (1,4 m/s) | Stick adelante/atrás: reptar | WASD |
| Girar | Stick der. (saltos de 30°) | Stick izq./der. (25°/s) | Ratón (tortuga: A/D) |
| Agarrar / soltar / lanzar | **Grip** (al soltar en movimiento se lanza) | — | E o clic izq. (con algo lanzable en la mano lanza hacia la mira) |
| Usar lo que sostienes (linterna, foto, palada, morder, enganchar luces) | **Gatillo** | — | F o clic der. |
| Pulsar botones (altavoz, fuegos) | Tocar con el dedo o gatillo cerca | — | Apuntar + E |
| Morder la "medusa" | — | Acercarse o gatillo | E |
| Impulso opcional | — | A / X | Espacio |
| Pausa: *Skip this activity*, checkpoint, reiniciar, menú | Botón Menú | Botón Menú | Esc / P |

**Recuperación:** cada actividad muestra de nuevo su pista si llevas 40 s sin avanzar, y el menú de pausa
permite **saltar la actividad** (se completa automáticamente, dejando sus consecuencias en la playa).
Como tortuga, *Back to the last checkpoint* te devuelve al camino.

## Modo Desktop Testing (Editor sin Meta Quest)

Se activa automáticamente al pulsar Play en el Editor si no hay ningún dispositivo XR funcionando
(o con `SAMESHORE_DESKTOP=1/0`, `-sameshore-desktop` / `-sameshore-xr`, o el componente `DesktopTestingMode`).
Usa el mismo `OVRCameraRig`; la mano derecha la sustituye una mano virtual delante de la cámara (`VRHand`)
que actúa sobre los mismos `VRGrabbable`, `PokeButton` e `IVRUsable` que en VR.

## Qué se ha implementado (por sistemas)

| Sistema | Scripts |
|---|---|
| Director narrativo (8 escenas, acto II, final, pausa, saltos) | `Core/ExperienceDirector.cs` |
| Agarre propio con pose, lanzamiento físico y modo escritorio (sustituye al agarre ISDK que torcía la linterna) | `Interaction/VRHand.cs`, `VRGrabbable.cs`, `VRGrab.cs`, `PokeButton.cs`, `FlashlightUse.cs` |
| Sentarse en la silla (altura de ojos, cuerpo sentado) | `Human/HumanSeat.cs`, `Human/HumanAvatar.cs` |
| Día → atardecer → noche (luz, ambiente, niebla, cielo, océano, luna, hotel) | `Story/DayCycle.cs`, `Shaders/SkyCycle.shader` |
| Actividades | `BeachDrink`, `MusicPlayer`, `CanTossGame`/`TossCan`, `SandcastleSite`/`BeachShovel`/`SandPail` (+ `SandHole`), `Sandwich`/`WindBag`, `FireworksShow`, `LightGarland`/`GarlandReel`, `PhoneCamera`, `PhotoHatchling` |
| Estado compartido | `Story/BeachLog.cs` |
| Acto II | `Story/TurtleStory.cs`, `GullEncounters.cs`, `TurtleHearing.cs`, `GiantHuman.cs`, `HumanPuppet.cs` (+ `TurtleController` del equipo, ampliado) |
| Escena | `Editor/SameShoreSceneBuilder.cs` + `SameShoreSceneBuilder.Story.cs`, mallas con `Core/MeshKit.cs` |

## Validación realizada (sin visor)

Ver la sección final de este documento (se actualiza con cada ejecución). **No se ha probado en unas
Quest 3 físicas:** todo se ha comprobado con Unity en modo batch.

- Compilación sin errores y `Same Shore → Validate Scene` sin scripts perdidos ni referencias nulas.
- Pruebas en Play Mode con entrada simulada (`Tools/Unity/run_batch.ps1 -Method SameShore.EditorTools.SameShoreSmokeTest.Run -NoQuit -Mode full|turtle|human|xr`),
  informe en `Logs/SameShoreSmokeTest.txt` y capturas en `Logs/Views/`.

### Resultado de la última validación (21-11-2026)

- Constructor + validador: **OK** (sin scripts perdidos ni referencias nulas).
- `-Mode turtle` (Acto II completo con playa por defecto, hasta el final y vuelta al menú): **19/22** en la
  primera pasada. Fallaron 3 comprobaciones y se corrigieron después (espera de las latas, la primera gaviota
  que no saltaba, la foto que se disparaba junto a la bolsa). **Falta volver a ejecutarla.**
- `-Mode full`: superan las escenas 1 a 3 hasta llenar el cubo (25 comprobaciones). La última ejecución se
  detuvo por **falta de memoria del sistema** a mitad del castillo, así que **las escenas 3 a 8 con entrada
  simulada y el recorrido completo están pendientes de volver a ejecutar**.
- `-Mode human` y `-Mode xr`: escritos, **sin ejecutar todavía**.

## Pendiente / requiere prueba real en Quest 3

- Confort y legibilidad en el visor; rendimiento en Android (sombras, bloom, luces de la guirnalda, la
  cámara del móvil, que solo renderiza mientras lo sostienes y a medio framerate).
- Orientación de la pose de la linterna, el móvil y la pala respecto al mando real (se usa la dirección del
  ancla del mando, la misma del láser de los menús); ajustable en `VRGrabbable` (pose).
- Lanzamiento de latas con mandos reales (multiplicador `throwScale` 1,12).
- No hay soporte específico de seguimiento de manos sin mandos.
