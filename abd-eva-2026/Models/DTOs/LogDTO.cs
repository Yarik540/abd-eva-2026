namespace abd_eva_2026.Models.DTOs
{
    public class LogDTO
    {
        public int idlog { get; set; }
        public string? accion { get; set; }
        public string? estado { get; set; }
        public string? mensajelog { get; set; }
        public DateTime? fechalog { get; set; }
        public int? latencia_ms { get; set; }
        public string? idusu { get; set; }
    }
}
