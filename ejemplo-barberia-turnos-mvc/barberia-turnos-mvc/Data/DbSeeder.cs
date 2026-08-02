using barberia_turnos_mvc.Data;
using barberia_turnos_mvc.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace barberia_turnos_mvc.Data
{
    public static class DbSeeder
    {
        public static void Seed(BarberiaDbContext context)
        {
            context.Database.EnsureCreated();

            if (context.Barberias.Any()) return; // ya hay datos, no volver a sembrar

            // === Barbería (una sola, según el alcance actual) ===

            var barberia = new Barberia
            {
                Nombre = "Barbería El Corte",
                Direccion = "Av. Siempre Viva 123",
                Telefono = "11-5555-0000",
                Slug = "elcorte"
            };

            context.Barberias.Add(barberia);
            context.SaveChanges(); // necesario para que barberia.Id se genere

            // === Servicios ===
            var servicios = new List<Servicio>
            {
                new Servicio { Nombre = "Corte clásico", Precio = 4500m, DuracionMinutos = 30, BarberiaId = barberia.Id },
                new Servicio { Nombre = "Corte + Barba", Precio = 7000m, DuracionMinutos = 45, BarberiaId = barberia.Id },
                new Servicio { Nombre = "Afeitado clásico", Precio = 3500m, DuracionMinutos = 20, BarberiaId = barberia.Id },
                new Servicio { Nombre = "Diseño de barba", Precio = 3000m, DuracionMinutos = 20, BarberiaId = barberia.Id }
            };
            context.Servicios.AddRange(servicios);
            context.SaveChanges(); // necesario para que cada Servicio.Id se genere

            // === Clientes ===
            var clientes = new List<Cliente>
            {
                new Cliente { Nombre = "Marcelo", Apellido = "Gómez", Telefono = "11-1111-1111", BarberiaId = barberia.Id },
                new Cliente { Nombre = "Juan", Apellido = "Pérez", Telefono = "11-2222-2222", BarberiaId = barberia.Id },
                new Cliente { Nombre = "Lucía", Apellido = "Fernández", Telefono = "11-3333-3333", BarberiaId = barberia.Id }
            };
            context.Clientes.AddRange(clientes);
            context.SaveChanges(); // necesario para que cada Cliente.Id se genere

            // === Turnos ===
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


        public static async Task SeedRolesYUsuarios(IServiceProvider serviceProvider)
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var context = serviceProvider.GetRequiredService<BarberiaDbContext>();

            string[] roles = { "Dueño", "Cliente" };

            foreach (var rol in roles)
            {
                if (!await roleManager.RoleExistsAsync(rol))
                {
                    await roleManager.CreateAsync(new IdentityRole(rol));
                }
            }

            var email = "dueno@barberiaelcorte.com";
            var usuarioExistente = await userManager.FindByEmailAsync(email);

            if (usuarioExistente == null)
            {
                // TODO: cuando haya más de una barbería, esto va a venir del
                // flujo de alta de un nuevo dueño, no de un FirstOrDefault fijo.
                var barberia = context.Barberias.FirstOrDefault();

                var dueno = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    NombreCompleto = "Marcelo (Dueño)",
                    EmailConfirmed = true,
                    BarberiaId = barberia?.Id
                };

                var resultado = await userManager.CreateAsync(dueno, "Barberia123!");

                if (resultado.Succeeded)
                {
                    await userManager.AddToRoleAsync(dueno, "Dueño");
                }
            }
        }
    }
}

