using Newtonsoft.Json;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace abd_eva_2026.Models
{
    [Table("consultas_agente")]
    public class ConsultaAgente : BaseModel
    {
        [PrimaryKey("idconsulta", false)]
        [Column("idconsulta")]
        [JsonProperty("idconsulta", NullValueHandling = NullValueHandling.Ignore)]
        public int? idconsulta { get; set; }

        [Column("pregunta")]
        public string? pregunta { get; set; }

        [Column("respuesta")]
        public string? respuesta { get; set; }

        [Column("fecha")]
        [JsonProperty("fecha", NullValueHandling = NullValueHandling.Ignore)]
        public DateTime? fecha { get; set; }

        [Column("idusu")]
        [JsonProperty("idusu", NullValueHandling = NullValueHandling.Ignore)]
        public string? idusu { get; set; } // UUID

        [Column("similitud")]
        [JsonProperty("similitud", NullValueHandling = NullValueHandling.Ignore)]
        public double? similitud { get; set; }

        [Column("tiempo_consulta_ms")]
        [JsonProperty("tiempo_consulta_ms", NullValueHandling = NullValueHandling.Ignore)]
        public int? tiempo_consulta_ms { get; set; }

        [Column("exito")]
        [JsonProperty("exito", NullValueHandling = NullValueHandling.Ignore)]
        public bool? exito { get; set; }
    }
}