namespace barberia_turnos_mvc.Helpers
{
    // Azure App Service corre en UTC sin importar la región elegida
    // (Brazil South no cambia el reloj del sistema operativo). Todas las
    // barberías de este SaaS son de Argentina, así que cualquier lugar
    // que necesite "la hora de ahora" para comparar contra horarios de
    // atención, vencimientos, etc. tiene que pasar por acá en vez de
    // usar DateTime.Now directo.
    public static class HoraArgentina
    {
        private static readonly TimeZoneInfo ZonaHoraria = ObtenerZonaHoraria();

        private static TimeZoneInfo ObtenerZonaHoraria()
        {
            // El ID de Windows y el de IANA (Linux) son distintos.
            // Probamos ambos para que funcione sin importar el SO del
            // App Service.
            try { return TimeZoneInfo.FindSystemTimeZoneById("Argentina Standard Time"); }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.FindSystemTimeZoneById("America/Argentina/Buenos_Aires");
            }
        }

        public static DateTime Ahora => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, ZonaHoraria);

        public static DateTime Hoy => Ahora.Date;
    }
}
