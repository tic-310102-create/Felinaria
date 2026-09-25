# Felinaria: El último presagio 🐱⚔️

RPG táctico 2D por turnos basado en cuadrículas (estilo *Fire Emblem* y *Final Fantasy Tactics*) desarrollado en **Unity C#**.

## 📌 Estado del Proyecto: Fase 3 (Entorno y Carga de Niveles)
- **GridManager:** Generación procedural del tablero 2D, reconstrucción dinámica y mapa de ocupación.
- **TileData & Terrenos:** Tipos de terreno configurables: Planicie, Obstáculo/Agua (intransitable para A*) y Bosque/Cobertura (+3 DEF, costo 2 MOV).
- **Pathfinding A\*:** Búsqueda de rutas con heurística Manhattan respetando transitabilidad y costes de terreno.
- **CombatSystem:** Cálculo de daño físico y mitigación de daño con bono de cobertura.
- **MapLoader:** Carga y serialización de batallas en tiempo de ejecución (Batalla 1 y Batalla 2 con vado de río y múltiples enemigos) con transiciones suaves (`SceneTransition`).
- **Persistencia (F5/F9):** Guardado y restauración de partidas en JSON con sincronización del mapa y unidades.

## 🛠️ Tecnologías
- **Motor:** Unity 2022 LTS+ (2D Core)
- **Lenguaje:** C# (.NET Standard)
- **Persistencia:** JSON (Local) / Preparado para sincronización móvil Android.

## 📂 Estructura de Assets
```
Assets/
├── Scripts/
│   ├── AI/         # IA Enemiga y Pathfinding A*
│   ├── Audio/      # Efectos de sonido y música
│   ├── Combat/     # Sistema de combate, daño y magia
│   ├── Data/       # Modelos de datos, skills y SaveSystem
│   ├── Grid/       # GridManager, TileData y MapLoader
│   ├── Managers/   # TurnManager, BattleManager, SceneTransition
│   ├── UI/         # ActionMenu, HealthBar, BattleResultUI, SaveLoadUI
│   └── Units/      # UnitController y UnitStats
├── Prefabs/        # Prefabs de Graybox y definitivos
└── Scenes/         # Escenas de batalla y menús
```
