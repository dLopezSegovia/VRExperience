# Same Shore: créditos y licencias de recursos

## Recursos externos añadidos en esta rama

Todos proceden de **Poly Haven** (https://polyhaven.com) y tienen licencia **CC0 1.0 (dominio público)**.
Pueden usarse, modificarse y redistribuirse sin atribución obligatoria; aun así los citamos.
Se descargaron a 1k, se optimizaron con `Tools/Blender/make_assets.py` (diezmado, mapas EXR → PNG y
roughness → mapa metallic/smoothness de URP) y se guardaron en `Assets/SameShore/Art/ThirdParty/PolyHaven/`.

| Asset | Uso en la escena | Autores | URL |
|---|---|---|---|
| coast_rocks_01 | Rocas grandes de la orilla | Rob Tuytel, Rico Cilliers | https://polyhaven.com/a/coast_rocks_01 |
| sand_rocks_small_01 | Rocas pequeñas, obstáculos | Rob Tuytel, Rico Cilliers | https://polyhaven.com/a/sand_rocks_small_01 |
| can_rusted | Latas (basura) | Rahul Chaudhary | https://polyhaven.com/a/can_rusted |
| small_plastic_torch | Modelo de la linterna | Abiyyu Panggalih | https://polyhaven.com/a/small_plastic_torch |
| lambis_shell | Conchas | Kuutti Siitonen | https://polyhaven.com/a/lambis_shell |
| dead_quiver_branch_01 | Madera de deriva, postes del nido | Greg Zaal, Jenelle van Heerden | https://polyhaven.com/a/dead_quiver_branch_01 |
| grass_medium_01 | Hierba de las dunas | Rob Tuytel, Rico Cilliers | https://polyhaven.com/a/grass_medium_01 |
| plastic_container | Envase de comida (basura) | PierreB3D | https://polyhaven.com/a/plastic_container |
| street_lamp_01 | Descargado; no se usa en la escena actual | Josh Dean | https://polyhaven.com/a/street_lamp_01 |

## Recursos creados para el proyecto (sin licencias de terceros)

- **Modelos** (`Assets/SameShore/Art/Models`): cría de tortuga con aletas articuladas, gaviota con alas
  separadas, botellas PET (entera y aplastada), botella de vidrio, bolsa de plástico y castillo de arena.
  Se modelan proceduralmente con `Tools/Blender/make_assets.py` (Blender 5.2).
- **Sonido** (`Assets/SameShore/Audio`): olas (normales y cercanas), viento, música lejana del hotel,
  voces amortiguadas, pisadas en arena, aletas, arrastre de arena, gaviotas, aleteo, latido, arena vertida,
  clic de linterna, golpes y transiciones. Se sintetizan con `Tools/Audio/synth_audio.py` (numpy/scipy).
- **Texturas y mallas generadas** (`Assets/SameShore/Generated`): luna creciente, fachada del hotel,
  huellas, interfaz, malla del océano y franja de orilla. Las crea el constructor de escena.
- **Shaders** (`Assets/SameShore/Shaders`): océano, oleaje en la orilla, fundido VR, visión de la cría,
  overlay de interfaz y objetos lejanos del cielo.

## Recursos que ya estaban en el proyecto

- Silla `plastic_monobloc_chair_01`, texturas de arena `aerial_beach_01` / `sand_03` y cielo
  `qwantani_night_puresky`: Poly Haven, CC0.
- `StylizedWoodenBucket` y `IgniteCoders/Simple Water Shader`: paquetes importados por el equipo. Revisad
  su licencia original (Unity Asset Store EULA) antes de redistribuirlos en un repositorio público.
  En la escena nueva el agua de IgniteCoders está desactivada (se sustituye por el océano propio).
- Texturas `hawksbill.*` en `Assets/3D Models/textures`: el modelo al que pertenecen no está en el
  repositorio, así que no se usan. No consta su origen ni su licencia.
- Fuente LiberationSans (TextMesh Pro): SIL Open Font License.
