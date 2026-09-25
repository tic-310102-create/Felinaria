// ============================================================
//  TileData.cs
//  Felinaria: El último presagio
//  Fase 3 – Entorno y Carga de Niveles
//
//  RESPONSABILIDAD:
//    - Define tipos de terreno: Planicie, Obstáculo/Agua y Cobertura/Bosque.
//    - Almacena modificadores de combate (bonus DEF, penalización MOV).
//    - Indica transitabilidad para el algoritmo A* y movimiento.
//    - Provee fábricas en tiempo de ejecución para auto-configuración pura.
// ============================================================

using UnityEngine;

namespace Felinaria.Grid
{
    /// <summary>
    /// Tipos estándar de terreno para el sistema táctico.
    /// </summary>
    public enum TipoTerreno
    {
        Planicie,           // Estándar (costo = 1, DEF +0, transitable)
        ObstaculoAgua,      // Río, muro o abismo (intransitable para A* y movimiento)
        CoberturaBosque     // Bosque o cobertura (costo = 2, DEF +3, transitable)
    }

    /// <summary>
    /// Ficha de tipo de terreno. Puede crearse como Asset o instanciarse en runtime.
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
        [Tooltip("Tipo categórico del terreno.")]
        public TipoTerreno Tipo = TipoTerreno.Planicie;

        [Tooltip("Nombre descriptivo del tipo de terreno.")]
        public string NombreTerreno = "Planicie";

        [Tooltip("Descripción breve para el tooltip al pasar el cursor.")]
        [TextArea(2, 3)]
        public string Descripcion = "Terreno plano sin modificadores.";

        // ── Transitabilidad ────────────────────────────────────────────────────
        [Header("Transitabilidad")]
        [Tooltip("¿Las unidades pueden caminar sobre esta celda? Desactivar para ríos, muros, etc.")]
        public bool EsTransitable = true;

        [Tooltip("Costo de movimiento para cruzar esta celda (1 = normal, 2 = difícil).")]
        [Range(1, 5)]
        public int CostoMovimiento = 1;

        // ── Modificadores de Combate ───────────────────────────────────────────
        [Header("Modificadores de Combate")]
        [Tooltip("Bonus a la Defensa de la unidad parada sobre esta celda.")]
        [Range(-10, 10)]
        public int BonusDefensa = 0;

        [Tooltip("Bonus al Ataque de la unidad parada sobre esta celda.")]
        [Range(-10, 10)]
        public int BonusAtaque = 0;

        [Tooltip("Bonus a la Evasión.")]
        [Range(-30, 30)]
        public int BonusEvasion = 0;

        // ── Visualización Graybox ──────────────────────────────────────────────
        [Header("Graybox Visual")]
        [Tooltip("Color que se aplica a la celda cuando tiene este terreno.")]
        public Color ColorTerreno = Color.white;

        [Tooltip("Si es true, se aplica el ColorTerreno a la celda.")]
        public bool AplicarColor = true;

        [Tooltip("Ícono opcional para mostrar en la celda.")]
        public Sprite Icono;

        // ── Fábrica Estática en Runtime ────────────────────────────────────────
        /// <summary>
        /// Crea una instancia en tiempo de ejecución para el tipo de terreno seleccionado.
        /// </summary>
        public static TileData CrearInstancia(TipoTerreno tipo)
        {
            var data = CreateInstance<TileData>();
            data.Tipo = tipo;

            switch (tipo)
            {
                case TipoTerreno.ObstaculoAgua:
                    data.NombreTerreno = "Agua / Obstáculo";
                    data.Descripcion = "Agua profunda e intransitable. Bloquea el paso.";
                    data.EsTransitable = false;
                    data.CostoMovimiento = 99;
                    data.BonusDefensa = 0;
                    data.BonusAtaque = 0;
                    data.ColorTerreno = new Color(0.18f, 0.42f, 0.82f, 0.95f); // Azul agua
                    data.AplicarColor = true;
                    break;

                case TipoTerreno.CoberturaBosque:
                    data.NombreTerreno = "Bosque / Cobertura";
                    data.Descripcion = "Vegetación densa. Ofrece +3 de Defensa pero cuesta 2 de Movimiento.";
                    data.EsTransitable = true;
                    data.CostoMovimiento = 2;
                    data.BonusDefensa = 3;
                    data.BonusAtaque = 0;
                    data.ColorTerreno = new Color(0.15f, 0.55f, 0.25f, 0.95f); // Verde bosque
                    data.AplicarColor = true;
                    break;

                case TipoTerreno.Planicie:
                default:
                    data.NombreTerreno = "Planicie";
                    data.Descripcion = "Terreno llano estándar sin modificadores.";
                    data.EsTransitable = true;
                    data.CostoMovimiento = 1;
                    data.BonusDefensa = 0;
                    data.BonusAtaque = 0;
                    data.ColorTerreno = Color.white;
                    data.AplicarColor = false;
                    break;
            }

            data.name = $"TileData_{tipo}";
            return data;
        }

        public static TileData CrearPlanicie() => CrearInstancia(TipoTerreno.Planicie);
        public static TileData CrearObstaculoAgua() => CrearInstancia(TipoTerreno.ObstaculoAgua);
        public static TileData CrearCoberturaBosque() => CrearInstancia(TipoTerreno.CoberturaBosque);

        // ── Utilidades ─────────────────────────────────────────────────────────
        /// <summary>
        /// Devuelve una descripción formateada de los modificadores activos.
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
