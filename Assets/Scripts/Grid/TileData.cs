// ============================================================
//  TileData.cs
//  Felinaria: El último presagio
//  Fase 3 – IA Enemiga y Mapa
//
//  RESPONSABILIDAD:
//    - ScriptableObject que define un TIPO de terreno (Bosque, Río, etc.).
//    - Almacena modificadores de combate (bonus DEF, penalización MOV).
//    - Indica si la celda es transitable o es un obstáculo bloqueante.
//    - Define el costo de movimiento (1 = normal, 2 = pantano, etc.).
//    - Se asigna a las celdas del GridManager para dar significado al mapa.
//
//  ¿CÓMO SE USA? (para principiantes):
//    1. Creas un TileData: Assets → Create → Felinaria → Tile Data.
//    2. Le pones nombre ("Bosque de los Susurros"), color y bonus.
//    3. En GridManager, asignas qué celdas usan ese tipo de terreno.
//    4. El Pathfinding y el CombatSystem leen estos datos automáticamente.
// ============================================================

using UnityEngine;

namespace Felinaria.Grid
{
    /// <summary>
    /// Ficha de tipo de terreno. Los diseñadores crean instancias desde:
    ///   Assets → Create → Felinaria → Tile Data
    /// </summary>
    [CreateAssetMenu(
        fileName = "NuevoTerreno",
        menuName = "Felinaria/Tile Data",
        order    = 1
    )]
    public class TileData : ScriptableObject
    {
        // ── Identidad ──────────────────────────────────────────────────────────
        [Header("Identidad del Terreno")]
        [Tooltip("Nombre del tipo de terreno (ej: 'Bosque de los Susurros').")]
        public string NombreTerreno = "Llanura";

        [Tooltip("Descripción breve para el tooltip al pasar el cursor.")]
        [TextArea(2, 3)]
        public string Descripcion = "Terreno plano sin modificadores.";

        // ── Transitabilidad ────────────────────────────────────────────────────
        [Header("Transitabilidad")]
        [Tooltip("¿Las unidades pueden caminar sobre esta celda? " +
                 "Desactivar para ríos, muros, abismos, etc.")]
        public bool EsTransitable = true;

        [Tooltip("Costo de movimiento para cruzar esta celda. " +
                 "1 = normal, 2 = terreno difícil (pantano, bosque denso). " +
                 "Se resta del rango de movimiento de la unidad.")]
        [Range(1, 5)]
        public int CostoMovimiento = 1;

        // ── Modificadores de Combate ───────────────────────────────────────────
        [Header("Modificadores de Combate")]
        [Tooltip("Bonus (o penalización si negativo) a la Defensa de la " +
                 "unidad que esté parada sobre esta celda.")]
        [Range(-10, 10)]
        public int BonusDefensa = 0;

        [Tooltip("Bonus (o penalización) al Ataque de la unidad " +
                 "que esté parada sobre esta celda.")]
        [Range(-10, 10)]
        public int BonusAtaque = 0;

        [Tooltip("Bonus (o penalización) a la Evasión (futuro).")]
        [Range(-30, 30)]
        public int BonusEvasion = 0;

        // ── Visualización Graybox ──────────────────────────────────────────────
        [Header("Graybox Visual")]
        [Tooltip("Color que se aplica a la celda cuando tiene este terreno. " +
                 "Sobreescribe el color de tablero de ajedrez.")]
        public Color ColorTerreno = Color.white;

        [Tooltip("Si es true, se aplica el ColorTerreno a la celda. " +
                 "Si es false, se conserva el color original del tablero.")]
        public bool AplicarColor = true;

        [Tooltip("Ícono opcional para mostrar en la celda (futuro).")]
        public Sprite Icono;

        // ── Utilidades ─────────────────────────────────────────────────────────
        /// <summary>
        /// Devuelve una descripción formateada de los modificadores activos.
        /// Útil para tooltips de la UI.
        /// </summary>
        public string ObtenerResumenModificadores()
        {
            string resumen = NombreTerreno;

            if (!EsTransitable)
            {
                resumen += " [INTRANSITABLE]";
                return resumen;
            }

            if (CostoMovimiento > 1)
                resumen += $" | MOV: x{CostoMovimiento}";

            if (BonusDefensa != 0)
                resumen += $" | DEF: {(BonusDefensa > 0 ? "+" : "")}{BonusDefensa}";

            if (BonusAtaque != 0)
                resumen += $" | ATK: {(BonusAtaque > 0 ? "+" : "")}{BonusAtaque}";

            if (BonusEvasion != 0)
                resumen += $" | EVA: {(BonusEvasion > 0 ? "+" : "")}{BonusEvasion}";

            return resumen;
        }
    }
}
