namespace barberia_turnos_mvc.Models
{
    public class EstadisticasViewModel
    {
        // Últimos 6 meses, en orden cronológico (más viejo primero) para
        // que el gráfico se lea de izquierda a derecha de forma natural.
        public List<string> MesesLabels { get; set; } = new();
        public List<decimal> IngresosPorMes { get; set; } = new();
        public List<int> TurnosPorMes { get; set; } = new();

        public List<RankingServicioDto> TopServicios { get; set; } = new();

        public int TotalTurnosUltimos6Meses { get; set; }
        public decimal IngresosUltimos6Meses { get; set; }

        // Null cuando todavía no hay turnos "pasados" (Completado o NoShow)
        // para calcular una tasa real.
        public double? TasaNoShow { get; set; }
    }

    public class RankingServicioDto
    {
        public string Nombre { get; set; } = string.Empty;
        public int Cantidad { get; set; }
        public decimal IngresoTotal { get; set; }
    }
}
