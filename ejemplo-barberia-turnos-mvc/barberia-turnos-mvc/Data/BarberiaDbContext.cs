using barberia_turnos_mvc.Models;
using Microsoft.EntityFrameworkCore;
using barberia_turnos_mvc;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace barberia_turnos_mvc.Data
{
    public class BarberiaDbContext : IdentityDbContext<ApplicationUser>
    {
        public BarberiaDbContext(DbContextOptions<BarberiaDbContext> options) : base(options) { }
        public DbSet<Barberia> Barberias { get; set; }
        public DbSet<BloqueoHorario> BloqueoHorarios { get; set; }
        public DbSet<Cliente> Clientes { get; set; }
        public DbSet<Servicio> Servicios { get; set; }
        public DbSet<Turno> Turnos { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Servicio>()
                .Property(s => s.Precio)
                .HasPrecision(10, 2);
            modelBuilder.Entity<Barberia>()
                .Property(b => b.PorcentajeSeña)
                .HasPrecision(5, 2);

            modelBuilder.Entity<Turno>()
                .Property(t => t.MontoSeña)
                .HasPrecision(10, 2);

            modelBuilder.Entity<Turno>()
                .HasOne(t => t.Cliente)
                .WithMany(c => c.Turnos)
                .HasForeignKey(t => t.ClienteId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Turno>()
                .HasOne(t => t.Barberia)
                .WithMany(b => b.Turnos)
                .HasForeignKey(t => t.BarberiaId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Turno>()
                .HasOne(t => t.Servicio)
                .WithMany()
                .HasForeignKey(t => t.ServicioId)
                .OnDelete(DeleteBehavior.Restrict);

            // Respaldo a nivel de base de datos contra el caso de dos
            // requests simultáneos pasando el chequeo de disponibilidad de
            // TurnoValidacionService al mismo tiempo (condición de carrera).
            // No reemplaza esa validación (que sí detecta turnos que se
            // SUPERPONEN en horarios distintos, algo que un índice no puede
            // expresar), pero sí garantiza, con la fuerza de un constraint
            // de base de datos, que dos turnos activos no puedan arrancar
            // en el EXACTO mismo horario dentro de la misma barbería —
            // que es precisamente el caso que una carrera de concurrencia
            // podría colar. Filtrado a estados activos (2=Cancelado y
            // 4=NoShow quedan afuera) para no bloquear que un horario
            // liberado se vuelva a reservar.
            modelBuilder.Entity<Turno>()
                .HasIndex(t => new { t.BarberiaId, t.FechaHora })
                .IsUnique()
                .HasFilter("[Estado] <> 2 AND [Estado] <> 4");

            modelBuilder.Entity<Barberia>()
                .HasIndex(b => b.Slug)
                .IsUnique();

            modelBuilder.Entity<Cliente>()
                .HasOne(c => c.Barberia)
                .WithMany(b => b.Clientes)
                .HasForeignKey(c => c.BarberiaId)
                .OnDelete(DeleteBehavior.Restrict);

            // Mismo teléfono puede existir en distintas barberías,
            // pero no puede repetirse DENTRO de la misma barbería.
            modelBuilder.Entity<Cliente>()
                .HasIndex(c => new { c.BarberiaId, c.Telefono })
                .IsUnique();

            modelBuilder.Entity<ApplicationUser>()
                .HasOne(u => u.Barberia)
                .WithMany()
                .HasForeignKey(u => u.BarberiaId)
                .OnDelete(DeleteBehavior.Restrict);

            base.OnModelCreating(modelBuilder);
        }

    }
}