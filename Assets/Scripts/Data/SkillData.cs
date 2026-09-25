// ============================================================
//  SkillData.cs
//  Felinaria: El último presagio
//  Fase 6 – Sistema de Habilidades Especiales y Magia
//
//  RESPONSABILIDAD:
//    - Define las propiedades de una habilidad o hechizo (ScriptableObject).
//    - Tipos de habilidad: Ataque Mágico, Curación, Ataque en Área (AoE), Buff.
//    - Formas de área: Puntual (1 celda), Cruz (5 celdas), Diamante/Cuadrado.
//    - Costos de maná, cooldowns, rangos Manhattan y potencia.
//    - Auto-generable por código para garantizar funcionamiento autónomo.
// ============================================================

using System;
using UnityEngine;

namespace Felinaria.Data
{
    public enum TipoHabilidad
    {
        AtaqueMagico,
        Curacion,
        AtaqueArea,
        BuffDefensa
    }

    public enum TipoObjetivo
    {
        Enemigo,
        Aliado,
        CualquierCelda,
        UnoMismo
    }

    public enum FormaArea
    {
        Puntual,    // Solo la celda seleccionada (radio 0)
        Cruz,       // Centro + 4 celdas adyacentes (radio 1)
        Diamante    // Área Manhattan de radio N
    }

    /// <summary>
    /// Ficha de datos para una habilidad o hechizo en Felinaria.
    /// Puede crearse en el Editor (Assets -> Create -> Felinaria -> Skill Data)
    /// o generarse automáticamente en runtime.
    /// </summary>
    [CreateAssetMenu(fileName = "NuevaHabilidad", menuName = "Felinaria/Skill Data", order = 1)]
    public class SkillData : ScriptableObject
    {
        [Header("Identificación")]
        public string IdHabilidad = "skill_basica";
        public string NombreHabilidad = "Habilidad";
        [TextArea(2, 3)]
        public string Descripcion = "Descripción de la habilidad.";
        public Sprite Icono;

        [Header("Clasificación")]
        public TipoHabilidad Tipo = TipoHabilidad.AtaqueMagico;
        public TipoObjetivo Objetivo = TipoObjetivo.Enemigo;
        public FormaArea Forma = FormaArea.Puntual;

        [Header("Costos y Tiempos")]
        [Range(0, 50)]
        public int CostoMana = 5;

        [Tooltip("Turnos de espera antes de poder volver a usar la habilidad (0 = sin cooldown).")]
        [Range(0, 10)]
        public int CooldownMaximo = 1;

        [Header("Alcance y Área")]
        [Tooltip("Distancia mínima Manhattan.")]
        [Range(0, 10)]
        public int RangoMinimo = 1;

        [Tooltip("Distancia máxima Manhattan.")]
        [Range(0, 10)]
        public int RangoMaximo = 3;

        [Tooltip("Radio de alcance del área (0 = solo celda objetivo).")]
        [Range(0, 3)]
        public int RadioArea = 0;

        [Header("Efectividad")]
        [Tooltip("Poder base de daño o curación.")]
        [Range(1, 999)]
        public int Potencia = 10;

        [Header("Visual")]
        public Color ColorEfecto = new Color(0.4f, 0.7f, 1f, 1f);

        /// <summary>
        /// Crea una instancia temporal en memoria con datos predefinidos para graybox/runtime.
        /// </summary>
        public static SkillData CrearInstanciaRuntime(
            string id, string nombre, string desc, TipoHabilidad tipo, TipoObjetivo obj,
            FormaArea forma, int mana, int cd, int rMin, int rMax, int radio, int potencia, Color color)
        {
            var skill = CreateInstance<SkillData>();
            skill.name = id;
            skill.IdHabilidad = id;
            skill.NombreHabilidad = nombre;
            skill.Descripcion = desc;
            skill.Tipo = tipo;
            skill.Objetivo = obj;
            skill.Forma = forma;
            skill.CostoMana = mana;
            skill.CooldownMaximo = cd;
            skill.RangoMinimo = rMin;
            skill.RangoMaximo = rMax;
            skill.RadioArea = radio;
            skill.Potencia = potencia;
            skill.ColorEfecto = color;
            return skill;
        }
    }
}
