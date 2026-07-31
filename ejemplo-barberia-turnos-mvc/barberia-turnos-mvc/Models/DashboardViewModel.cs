namespace barberia_turnos_mvc.Models
{
    public class DashboardViewModel
    {
        public List<Turno> TurnosDeHoy { get; set; } = new();
        public decimal IngresosDelMes { get; set; }
        public int TurnosPendientesDePago { get; set; }
        public int TurnosConfirmadosHoy { get; set; }
    }
}