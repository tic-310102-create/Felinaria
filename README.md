# Felinaria: El último presagio 🐱⚔️

RPG táctico 2D por turnos basado en cuadrículas (estilo *Fire Emblem* y *Final Fantasy Tactics*) desarrollado en **Unity C#**.

## 📌 Estado del Proyecto: Fase 1 (Grayboxing Core)
- **GridManager:** Generación procedural del tablero y mapa de ocupación.
- **UnitController:** Movimiento interpolado de unidades, límites de cuadrícula y control de turno.
- **TurnManager:** Máquina de estados para turnos de Jugador y Enemigo.

## 🛠️ Tecnologías
- **Motor:** Unity 2022 LTS+ (2D Core)
- **Lenguaje:** C# (.NET Standard)
- **Persistencia:** JSON (Local) / Preparado para sincronización móvil Android.

## 📂 Estructura de Assets
```
Assets/
├── Scripts/
│   ├── Grid/       # Lógica del tablero y celdas
│   ├── Units/      # Controladores de personajes y stats
│   ├── Managers/   # Gestores globales (Turnos, Combate)
│   └── Data/       # Modelos y persistencia JSON
├── Prefabs/        # Prefabs de Graybox y definitivos
└── Scenes/         # Escenas de batalla y menús
```
