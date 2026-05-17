using Newtonsoft.Json;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace abd_eva_2026.Models
{
    [Table("logs")]
    public class Log : BaseModel
    {
        [PrimaryKey("idlog", false)]
        [Column("idlog")]
        [JsonProperty("idlog", NullValueHandling = NullValueHandling.Ignore)]
        public int? idlog { get; set; }

        [Column("idusu")]
        [JsonProperty("idusu", NullValueHandling = NullValueHandling.Ignore)]
        public string? idusu { get; set; }  // UUID

        [Column("accion")]
        public string? accion { get; set; }

        [Column("estado")]
        public string? estado { get; set; }

        [Column("latencia_ms")]
        [JsonProperty("latencia_ms", NullValueHandling = NullValueHandling.Ignore)]
        public int? latencia_ms { get; set; }

        [Column("mensajelog")]
        public string? mensajelog { get; set; }

        [Column("fechalog")]
        [JsonProperty("fechalog", NullValueHandling = NullValueHandling.Ignore)]
        public DateTime? fechalog { get; set; }
    }
}