# 🌼 Goldy Pond

Videojuego 3D de **aventura, fantasía y supervivencia** en primera persona, desarrollado en **Unity** con modelos hechos en **Blender**.

> Una joven despierta en una aldea desconocida, llena de color y belleza... pero una vez que alguien entra a Goldy Pond, ya no puede salir.

---

## 📖 Historia

La protagonista despierta misteriosamente en una aldea rodeada de casitas, habitantes amigables, bosques, flores y molinos de viento. Al explorarla descubre su oscuro secreto: cada cierto tiempo suena una **melodía suave** que anuncia la llegada de los **Guardianes**, criaturas que persiguen a los habitantes durante las llamadas **cacerías**. Cuando la música termina, desaparecen hasta la siguiente.

Para escapar, deberá encontrar pistas, recolectar objetos y activar mecanismos que la lleven a una entrada secreta dentro de un molino, donde se encuentra un antiguo **elevador subterráneo**: la única salida de la aldea.

## 🎯 Objetivo

Escapar de la aldea explorando, recolectando objetos y activando mecanismos mientras evitas a los Guardianes.

1. Investigar las viviendas y encontrar pistas.
2. Conseguir la **llave del molino** y el **engranaje** del mecanismo secreto.
3. Abrir el pasadizo y descubrir el elevador subterráneo.
4. Encontrar y activar las **tres palancas** repartidas por la aldea.
5. Sobrevivir a la persecución final del **Guardián Mayor** y llegar al elevador.

### 🏆 Ganar y perder

- **Victoria:** completar los objetivos y llegar al elevador sin ser capturada. La protagonista abandona la aldea... y despierta en su cama, como si nada hubiera pasado.
- **Derrota:** un Guardián alcanza a la protagonista. Aparece la pantalla de derrota con la opción de reintentar.

## 🎮 Mecánicas

| Mecánica | Descripción |
|---|---|
| Movimiento | Primera persona: caminar, correr y explorar. |
| Cacerías | Una melodía anuncia a los Guardianes; persiguen al jugador y se van cuando termina la música. |
| Interacción | Recoger objetos, abrir puertas, leer pistas y activar mecanismos. |
| Pistas | Mensajes, pergaminos y mapas que guían hacia el molino, el pasadizo y el elevador. |
| Linterna | Ilumina zonas oscuras de noche y en el subterráneo. Se prende y apaga sin baterías. |
| Radar | Se obtiene en el molino; detecta Guardianes cercanos dentro de un rango. |
| Combate | Arma de fuego con munición limitada. Los Guardianes normales caen temporalmente y reaparecen en la siguiente cacería. |
| Municiones | Solo 2 bolsas de 10 balas (**20 balas en toda la partida**). |
| Jefe final | El Guardián Mayor no puede morir; los disparos solo lo frenan un momento. |
| Ciclo día/noche | Cambia la ambientación y la tensión de la exploración. |

## 🗺️ Contenido del juego

**Escenarios**
- Aldea principal (viviendas, caminos, jardines y vegetación)
- Bosque con varias rutas de exploración
- Molino de viento con mecanismo secreto
- Refugio oculto dentro del molino
- Pasadizo hacia el subterráneo
- Caverna subterránea con agua, flores y el elevador

**Personajes**
- Protagonista
- NPC habitantes de la aldea
- Guardianes básicos del bosque
- Cazadores avanzados
- Guardián Mayor (jefe final)

**Objetos coleccionables**
- Llave del molino
- Engranaje del mecanismo secreto
- Linterna
- Radar de proximidad
- Arma de fuego
- 2 bolsas de municiones (10 balas c/u)

**Objetos interactivos**
- Puertas y mecanismos
- Letreros y mensajes escritos
- Pergamino con información sobre la salida
- Mapa con la ubicación de las palancas
- 3 palancas de activación
- Elevador subterráneo

**Interfaz**
- Menú principal y menú de pausa
- Indicadores de interacción y mensajes de objetivos
- Contador de municiones y palancas
- Pantallas de victoria y derrota

## 🎨 Estilo visual

- Estilo 3D **estilizado**, con personajes de apariencia animada.
- Aldea de cuento de hadas con colores vivos, mucha vegetación y construcciones antiguas.
- Guardianes con estética de **fantasía oscura**: máscaras, cuernos, capas y elementos sobrenaturales que contrastan con la aldea.
- Subterráneo que mezcla agua cristalina, cascadas y flores con estructuras mecánicas antiguas.

**Inspiración:** *The Promised Neverland* (la reserva de caza y las cacerías anunciadas con música) y juegos de aventura basados en exploración, pistas y mecanismos.

## 🛠️ Tecnologías

- **Unity** – motor del juego: escenarios, mecánicas, IA de enemigos, interfaz.
- **Blender** – modelado, materiales y animación.
- **C#** – scripts de Unity.
- **IA generativa** – solo para imágenes de referencia en la etapa de concepto (ver abajo).

## 👥 Equipo

| Integrante | Responsabilidades |
|---|---|
| **Camila Andrea Jaimes Martínez** | Modelado, materiales y animación de personajes (protagonista, NPC, Guardianes y Guardián Mayor) · Integración y pruebas |
| **Luis Armando Figueroa Baeza** | Programación: movimiento del jugador, sistema de cacerías, rutas de los Guardianes, combate y municiones, Guardián Mayor, linterna, radar, ciclo día/noche, menús y transiciones · Menú principal, pausa y pantallas de victoria/derrota · Documentación · Integración y pruebas |
| **Paulina Cancino Vichi** | Modelado, materiales y animación de escenarios, objetos y mecanismos · Level design · Programación de interacción, recolección, progreso, mecanismos, elevador y NPC · Interfaz de partida (HUD) · Música y efectos de sonido · Integración y pruebas |

Descripción general, objetivos y progresión de la partida: Camila Andrea y Paulina Cancino.

## 🤖 Uso de IA

Se usaron herramientas de IA generativa **únicamente** como apoyo en la etapa de conceptualización, para generar imágenes de referencia de escenarios, personajes, enemigos y objetos. Estas imágenes sirvieron solo como inspiración.

El diseño, modelado, animación, programación e implementación de las mecánicas fueron hechos por los integrantes del equipo.

## ▶️ Cómo abrir el proyecto

1. Clona el repositorio:
   ```bash
   git clone <url-del-repo>
   ```
2. Abre **Unity Hub** → *Add project from disk* → selecciona la carpeta del proyecto.
3. Usa la misma versión de Unity con la que se creó el proyecto (Unity Hub te la indicará).
4. Abre la escena principal y presiona **Play**.

## 🎓 Créditos académicos

Proyecto para la asignatura **Programación y animación 3D** (3-2A)
Docente: **Perla Sofía Márquez Castro**
Ingeniería en Software y Minería de Datos
**Universidad Autónoma de Guadalajara, Campus Tabasco**
Villahermosa, Tabasco, México · 2026
