using barberia_turnos_mvc.Data;
using barberia_turnos_mvc.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace barberia_turnos_mvc.Data
{
    public static class DbSeeder
    {
        // Roles necesarios para que Identity funcione. Se corre en
        // cualquier ambiente, dev o producción.
        public static async Task SeedRoles(IServiceProvider serviceProvider)
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            string[] roles = { "Dueño", "Cliente" };
            foreach (var rol in roles)
            {
                if (!await roleManager.RoleExistsAsync(rol))
                {
                    await roleManager.CreateAsync(new IdentityRole(rol));
                }
            }
        }

        // Barbería + servicios + clientes + turnos de ejemplo.
        // SOLO para desarrollo (ver el chequeo IsDevelopment en Program.cs).
        public static void SeedDatosDeEjemplo(BarberiaDbContext context)
        {
            if (context.Barberias.Any()) return; // ya hay datos, no volver a sembrar

            var barberia = new Barberia
            {
                Nombre = "Barbería El Corte",
                Direccion = "Av. Siempre Viva 123",
                Telefono = "11-5555-0000",
                Slug = "elcorte"
            };

            context.Barberias.Add(barberia);
            context.SaveChanges(); // necesario para que barberia.Id se genere

            var servicios = new List<Servicio>
            {
                new Servicio { Nombre = "Corte clásico", Precio = 4500m, DuracionMinutos = 30, BarberiaId = barberia.Id },
                new Servicio { Nombre = "Corte + Barba", Precio = 7000m, DuracionMinutos = 45, BarberiaId = barberia.Id },
                new Servicio { Nombre = "Afeitado clásico", Precio = 3500m, DuracionMinutos = 20, BarberiaId = barberia.Id },
                new Servicio { Nombre = "Diseño de barba", Precio = 3000m, DuracionMinutos = 20, BarberiaId = barberia.Id }
            };
            context.Servicios.AddRange(servicios);
            context.SaveChanges();

            var clientes = new List<Cliente>
            {
                new Cliente { Nombre = "Marcelo", Apellido = "Gómez", Telefono = "11-1111-1111", BarberiaId = barberia.Id },
                new Cliente { Nombre = "Juan", Apellido = "Pérez", Telefono = "11-2222-2222", BarberiaId = barberia.Id },
                new Cliente { Nombre = "Lucía", Apellido = "Fernández", Telefono = "11-3333-3333", BarberiaId = barberia.Id }
            };
            context.Clientes.AddRange(clientes);
            context.SaveChanges();

            var turnos = new List<Turno>
            {
                new Turno
                {
                    FechaHora = DateTime.Today.AddDays(1).AddHours(10),
                    Estado = EstadoTurno.Pendiente,
                    ClienteId = clientes[0].Id,
                    BarberiaId = barberia.Id,
                    ServicioId = servicios[0].Id
                },
                new Turno
                {
                    FechaHora = DateTime.Today.AddDays(1).AddHours(11),
                    Estado = EstadoTurno.Confirmado,
                    ClienteId = clientes[1].Id,
                    BarberiaId = barberia.Id,
                    ServicioId = servicios[1].Id
                },
                new Turno
                {
                    FechaHora = DateTime.Today.AddDays(2).AddHours(15),
                    Estado = EstadoTurno.Pendiente,
                    ClienteId = clientes[2].Id,
                    BarberiaId = barberia.Id,
                    ServicioId = servicios[2].Id
                }
            };
            context.Turnos.AddRange(turnos);
            context.SaveChanges();
        }

        // Usuario Dueño de ejemplo con contraseña conocida.
        // SOLO para desarrollo — NUNCA correr esto en producción,
        // la contraseña es pública (está en el historial de git).
        public static async Task SeedDuenoDeEjemplo(IServiceProvider serviceProvider)
        {
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var context = serviceProvider.GetRequiredService<BarberiaDbContext>();

            var email = "dueno@barberiaelcorte.com";
            var usuarioExistente = await userManager.FindByEmailAsync(email);
            if (usuarioExistente != null) return;

            var barberia = context.Barberias.FirstOrDefault(b => b.Slug == "elcorte");
            if (barberia == null) return;

            var dueno = new ApplicationUser
            {
                UserName = email,
                Email = email,
                NombreCompleto = "Marcelo (Dueño)",
                EmailConfirmed = true,
                BarberiaId = barberia.Id
            };

            var resultado = await userManager.CreateAsync(dueno, "Barberia123!");
            if (resultado.Succeeded)
            {
                await userManager.AddToRoleAsync(dueno, "Dueño");
            }
        }
    }
}
