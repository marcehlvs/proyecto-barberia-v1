using barberia_turnos_mvc.Models;
using Microsoft.EntityFrameworkCore;
using barberia_turnos_mvc;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace barberia_turnos_mvc.Data
{
    public class BarberiaDbContext: IdentityDbContext<ApplicationUser>
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

            base.OnModelCreating(modelBuilder);
        }

    }
}
