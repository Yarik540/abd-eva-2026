using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using Newtonsoft.Json;

namespace abd_eva_2026.Models
{
    [Table("registros")]
    //solucionar error;
    public class Registro : BaseModel
    {
        [PrimaryKey("idreg", false)]                                 
        [Column("idreg")]
        public int idreg { get; set; }

        [Column("contenidoreg")]
        public string contenidoreg { get; set; }

        [Column("fechareg")]
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]  
        public DateTime? fechareg { get; set; }

        [Column("idusu")]
        public int idusu { get; set; }
    }
}