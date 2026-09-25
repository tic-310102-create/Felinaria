// ============================================================
//  Pathfinding.cs
//  Felinaria: El último presagio
//  Fase 3 – IA Enemiga y Mapa
//
//  RESPONSABILIDAD:
//    - Implementa A* (A-Star) sobre la cuadrícula Manhattan (sin diagonales).
//    - Devuelve la ruta más corta entre dos celdas evadiendo obstáculos.
//    - Respeta el costo de movimiento de TileData (bosque = costo 2, etc.).
//    - Expone API para la IA y para el ActionMenu (previsualización de ruta).
//    - Puede limitar la búsqueda al rango de movimiento de una unidad.
//
//  ¿QUÉ ES A*? (para principiantes):
//    A* explora las celdas vecinas priorizando las que están más cerca
//    del destino (heurística Manhattan). Es más eficiente que BFS porque
//    no explora todo el tablero: "mira primero hacia donde quieres ir".
//    Cuando encuentra el destino, reconstruye la ruta caminando hacia atrás
//    por los nodos "padre" hasta llegar al origen.
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using Felinaria.Grid;

namespace Felinaria.AI
{
    // ─── Nodo interno del A* ─────────────────────────────────────────────────
    /// <summary>
    /// Nodo temporal usado durante la búsqueda A*.
    /// No se expone fuera de Pathfinding.
    /// </summary>
    internal class NodoAStar
    {
        /// <summary>Coordenada de esta celda en la cuadrícula.</summary>
        public Vector2Int Coordenada;

        /// <summary>Nodo del que venimos (para reconstruir la ruta).</summary>
        public NodoAStar Padre;

        /// <summary>
        /// G = costo acumulado desde el origen hasta este nodo.
        /// Incluye costos de terreno (CostoMovimiento de TileData).
        /// </summary>
        public int G;

        /// <summary>
        /// H = heurística: estimación del costo restante hasta el destino.
        /// Usamos distancia Manhattan como heurística admisible.
        /// </summary>
        public int H;

        /// <summary>
        /// F = G + H. El nodo con menor F se explora primero.
        /// </summary>
        public int F => G + H;

        public NodoAStar(Vector2Int coord, NodoAStar padre, int g, int h)
        {
            Coordenada = coord;
            Padre      = padre;
            G          = g;
            H          = h;
        }
    }

    // ─── Pathfinding ─────────────────────────────────────────────────────────
    /// <summary>
    /// Sistema estático de pathfinding A* para la cuadrícula táctica.
    /// No necesita ser MonoBehaviour; se llama directamente con métodos estáticos.
    /// </summary>
    public static class Pathfinding
    {
        // Las 4 direcciones cardinales (sin diagonales, estilo Fire Emblem).
        private static readonly Vector2Int[] Direcciones = new Vector2Int[]
        {
            new Vector2Int( 1,  0),   // Derecha
            new Vector2Int(-1,  0),   // Izquierda
            new Vector2Int( 0,  1),   // Arriba
            new Vector2Int( 0, -1),   // Abajo
        };

        // ── API pública ────────────────────────────────────────────────────────

        /// <summary>
        /// Busca la ruta más corta desde 'origen' hasta 'destino'
        /// usando el algoritmo A* con heurística Manhattan.
        ///
        /// Retorna una lista ordenada de coordenadas (sin incluir el origen).
        /// Si no hay ruta posible, retorna una lista vacía.
        /// </summary>
        /// <param name="origen">Coordenada de inicio (donde está la unidad).</param>
        /// <param name="destino">Coordenada objetivo.</param>
        /// <param name="ignorarOcupacion">
        /// Si es true, las celdas ocupadas por unidades se consideran transitables.
        /// Útil para calcular distancia ignorando unidades temporales.
        /// La celda DESTINO siempre se ignora (puede estar ocupada por el enemigo a atacar).
        /// </param>
        /// <returns>Lista de coordenadas de la ruta (sin incluir el origen).</returns>
        public static List<Vector2Int> BuscarRuta(Vector2Int origen, Vector2Int destino,
                                                   bool ignorarOcupacion = false)
        {
            if (GridManager.Instancia == null)
            {
                Debug.LogError("[Pathfinding] No se encontró GridManager.");
                return new List<Vector2Int>();
            }

            // Si origen == destino, ya llegamos.
            if (origen == destino)
                return new List<Vector2Int>();

            // Verificar que el destino existe en la cuadrícula.
            if (!GridManager.Instancia.EsCoordenadaValida(destino))
            {
                Debug.LogWarning($"[Pathfinding] Destino {destino} fuera del tablero.");
                return new List<Vector2Int>();
            }

            // ── Estructuras del A* ───────────────────────────────────────────
            // Lista abierta: nodos por explorar, ordenados por F.
            var abierta = new List<NodoAStar>();

            // Set cerrado: coordenadas ya exploradas.
            var cerrada = new HashSet<Vector2Int>();

            // Crear nodo inicial.
            int hInicial = DistanciaManhattan(origen, destino);
            var nodoInicial = new NodoAStar(origen, null, 0, hInicial);
            abierta.Add(nodoInicial);

            // ── Bucle principal del A* ────────────────────────────────────────
            while (abierta.Count > 0)
            {
                // Encontrar el nodo con menor F en la lista abierta.
                var actual = ObtenerMejorNodo(abierta);
                abierta.Remove(actual);

                // ¿Llegamos al destino?
                if (actual.Coordenada == destino)
                {
                    return ReconstruirRuta(actual);
                }

                // Marcar como explorado.
                cerrada.Add(actual.Coordenada);

                // ── Explorar vecinos en las 4 direcciones ────────────────────
                foreach (var dir in Direcciones)
                {
                    Vector2Int vecino = actual.Coordenada + dir;

                    // Saltar si ya fue explorado.
                    if (cerrada.Contains(vecino)) continue;

                    // Saltar si no es una coordenada válida del tablero.
                    if (!GridManager.Instancia.EsCoordenadaValida(vecino)) continue;

                    // Obtener datos de la celda.
                    Cell celda = GridManager.Instancia.ObtenerCelda(vecino);
                    if (celda == null) continue;

                    // Obtener TileData del terreno (si existe).
                    TileData terreno = GridManager.Instancia.ObtenerTileData(vecino);

                    // ¿Es transitable?
                    if (terreno != null && !terreno.EsTransitable) continue;

                    // ¿Está ocupada? (Excepto si es el destino final o ignoramos ocupación.)
                    if (!ignorarOcupacion && vecino != destino && celda.EstaOcupada) continue;

                    // Calcular costo de movimiento.
                    int costoTerreno = (terreno != null) ? terreno.CostoMovimiento : 1;
                    int gNuevo = actual.G + costoTerreno;

                    // Verificar si ya está en la lista abierta con mejor G.
                    var nodoExistente = abierta.Find(n => n.Coordenada == vecino);
                    if (nodoExistente != null)
                    {
                        // Si encontramos un camino mejor, actualizar.
                        if (gNuevo < nodoExistente.G)
                        {
                            nodoExistente.G = gNuevo;
                            nodoExistente.Padre = actual;
                        }
                        continue;
                    }

                    // Crear nuevo nodo y agregarlo a la lista abierta.
                    int hNuevo = DistanciaManhattan(vecino, destino);
                    var nuevoNodo = new NodoAStar(vecino, actual, gNuevo, hNuevo);
                    abierta.Add(nuevoNodo);
                }
            }

            // No se encontró ruta.
            Debug.Log($"[Pathfinding] No hay ruta de {origen} a {destino}.");
            return new List<Vector2Int>();
        }

        /// <summary>
        /// Busca la ruta más corta LIMITADA al rango de movimiento de la unidad.
        /// Si el destino está más lejos que el rango, devuelve la ruta hasta
        /// la celda más cercana al destino que sí esté dentro del rango.
        /// </summary>
        /// <param name="origen">Coordenada de inicio.</param>
        /// <param name="destino">Coordenada objetivo ideal.</param>
        /// <param name="rangoMaximo">Celdas máximas que puede recorrer (costo acumulado).</param>
        /// <returns>Ruta recortada al rango de movimiento.</returns>
        public static List<Vector2Int> BuscarRutaConRango(Vector2Int origen, Vector2Int destino,
                                                           int rangoMaximo)
        {
            var rutaCompleta = BuscarRuta(origen, destino);

            if (rutaCompleta.Count == 0) return rutaCompleta;

            // Recortar la ruta al rango de movimiento.
            var rutaRecortada = new List<Vector2Int>();
            int costoAcumulado = 0;

            foreach (var paso in rutaCompleta)
            {
                TileData terreno = GridManager.Instancia?.ObtenerTileData(paso);
                int costoTerreno = (terreno != null) ? terreno.CostoMovimiento : 1;

                costoAcumulado += costoTerreno;

                if (costoAcumulado > rangoMaximo) break;

                rutaRecortada.Add(paso);
            }

            return rutaRecortada;
        }

        /// <summary>
        /// Devuelve TODAS las celdas alcanzables desde un origen dentro
        /// de un rango dado, respetando costos de terreno y obstáculos.
        /// Usa BFS (Breadth-First Search) con costos.
        /// Útil para resaltar las celdas de movimiento en el ActionMenu.
        /// </summary>
        /// <param name="origen">Celda desde donde se calcula.</param>
        /// <param name="rango">Rango máximo de movimiento.</param>
        /// <returns>Set de coordenadas alcanzables.</returns>
        public static HashSet<Vector2Int> ObtenerCeldasAlcanzables(Vector2Int origen, int rango)
        {
            var alcanzables = new HashSet<Vector2Int>();
            var costos = new Dictionary<Vector2Int, int>();
            var cola = new Queue<Vector2Int>();

            cola.Enqueue(origen);
            costos[origen] = 0;

            while (cola.Count > 0)
            {
                var actual = cola.Dequeue();

                foreach (var dir in Direcciones)
                {
                    Vector2Int vecino = actual + dir;

                    if (!GridManager.Instancia.EsCoordenadaValida(vecino)) continue;

                    Cell celda = GridManager.Instancia.ObtenerCelda(vecino);
                    if (celda == null || celda.EstaOcupada) continue;

                    TileData terreno = GridManager.Instancia.ObtenerTileData(vecino);
                    if (terreno != null && !terreno.EsTransitable) continue;

                    int costoTerreno = (terreno != null) ? terreno.CostoMovimiento : 1;
                    int costoNuevo = costos[actual] + costoTerreno;

                    if (costoNuevo > rango) continue;

                    // Si ya visitamos este vecino con un costo menor, saltar.
                    if (costos.ContainsKey(vecino) && costos[vecino] <= costoNuevo) continue;

                    costos[vecino] = costoNuevo;
                    alcanzables.Add(vecino);
                    cola.Enqueue(vecino);
                }
            }

            return alcanzables;
        }

        // ── Utilidades internas ────────────────────────────────────────────────

        /// <summary>
        /// Distancia Manhattan = |Δx| + |Δy|. Heurística admisible
        /// para cuadrículas sin movimiento diagonal.
        /// </summary>
        private static int DistanciaManhattan(Vector2Int a, Vector2Int b)
            => Mathf.Abs(b.x - a.x) + Mathf.Abs(b.y - a.y);

        /// <summary>
        /// Encuentra el nodo con menor F en la lista abierta.
        /// En caso de empate, prefiere el nodo con menor H (más cercano al destino).
        /// </summary>
        private static NodoAStar ObtenerMejorNodo(List<NodoAStar> lista)
        {
            NodoAStar mejor = lista[0];
            for (int i = 1; i < lista.Count; i++)
            {
                if (lista[i].F < mejor.F ||
                    (lista[i].F == mejor.F && lista[i].H < mejor.H))
                {
                    mejor = lista[i];
                }
            }
            return mejor;
        }

        /// <summary>
        /// Reconstruye la ruta caminando desde el nodo destino hacia atrás
        /// a través de los nodos Padre, y la invierte para obtener el orden correcto.
        /// No incluye el nodo de origen en la ruta resultante.
        /// </summary>
        private static List<Vector2Int> ReconstruirRuta(NodoAStar nodoFinal)
        {
            var ruta = new List<Vector2Int>();
            var actual = nodoFinal;

            while (actual.Padre != null)
            {
                ruta.Add(actual.Coordenada);
                actual = actual.Padre;
            }

            // La ruta se construyó al revés (destino → origen), invertirla.
            ruta.Reverse();
            return ruta;
        }
    }
}
