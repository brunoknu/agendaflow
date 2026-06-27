using AgendaFlow.Domain.Entities;
using AgendaFlow.Domain.Enums;
using AgendaFlow.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgendaFlow.Infrastructure.Persistence;

public static class DataSeeder
{
    public static async Task SeedAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>();

        await db.Database.MigrateAsync(ct);

        if (await db.Tenants.AnyAsync(ct))
        {
            logger.LogInformation("Database already seeded. Skipping.");
            return;
        }

        logger.LogInformation("Seeding development data for Clínica Horizonte...");

        // ── Tenant ────────────────────────────────────────────────
        var tenant = Tenant.Create("Clínica Horizonte", "horizonte", "America/Sao_Paulo");
        await db.Tenants.AddAsync(tenant, ct);

        // ── Admin user ────────────────────────────────────────────
        var adminUser = new ApplicationUser
        {
            UserName = "admin@horizonte.com",
            Email = "admin@horizonte.com",
            FullName = "Administrador Horizonte",
            EmailConfirmed = true
        };
        var result = await userManager.CreateAsync(adminUser, "Admin@1234");
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to create seed admin user: {errors}");
        }

        await db.TenantMemberships.AddAsync(
            TenantMembership.Create(tenant.Id, adminUser.Id, MembershipRole.Owner), ct);

        // ── Staff user ────────────────────────────────────────────
        var staffUser = new ApplicationUser
        {
            UserName = "recepcao@horizonte.com",
            Email = "recepcao@horizonte.com",
            FullName = "Recepção Horizonte",
            EmailConfirmed = true
        };
        var staffResult = await userManager.CreateAsync(staffUser, "Staff@1234");
        if (staffResult.Succeeded)
        {
            await db.TenantMemberships.AddAsync(
                TenantMembership.Create(tenant.Id, staffUser.Id, MembershipRole.Staff), ct);
        }

        // ── Services ──────────────────────────────────────────────
        var consultaClinica = Service.Create(
            tenant.Id, "Consulta Clínica Geral",
            "Atendimento clínico geral com médico especialista",
            durationMinutes: 30, price: 200m, currency: "BRL",
            bufferAfterMinutes: 10);

        var retorno = Service.Create(
            tenant.Id, "Retorno",
            "Consulta de retorno pós-tratamento",
            durationMinutes: 20, price: 100m, currency: "BRL");

        var exameRotina = Service.Create(
            tenant.Id, "Exame de Rotina",
            "Coleta e avaliação de exames laboratoriais",
            durationMinutes: 45, price: 250m, currency: "BRL",
            bufferBeforeMinutes: 5, bufferAfterMinutes: 15);

        var checkUp = Service.Create(
            tenant.Id, "Check-up Completo",
            "Avaliação completa de saúde com exames e consulta",
            durationMinutes: 60, price: 450m, currency: "BRL",
            bufferAfterMinutes: 15);

        await db.Services.AddRangeAsync([consultaClinica, retorno, exameRotina, checkUp], ct);

        // ── Professionals ─────────────────────────────────────────
        var drAna = Professional.Create(
            tenant.Id, "Dra. Ana Lima",
            "ana.lima@horizonte.com", "(11) 99100-0001");

        var drCarlos = Professional.Create(
            tenant.Id, "Dr. Carlos Mendes",
            "carlos.mendes@horizonte.com", "(11) 99100-0002");

        var drFernanda = Professional.Create(
            tenant.Id, "Dra. Fernanda Rocha",
            "fernanda.rocha@horizonte.com", "(11) 99100-0003");

        await db.Professionals.AddRangeAsync([drAna, drCarlos, drFernanda], ct);

        // ── Professional ↔ Service links ──────────────────────────
        await db.ProfessionalServices.AddRangeAsync([
            ProfessionalService.Create(drAna.Id, consultaClinica.Id),
            ProfessionalService.Create(drAna.Id, retorno.Id),
            ProfessionalService.Create(drAna.Id, checkUp.Id),
            ProfessionalService.Create(drCarlos.Id, consultaClinica.Id),
            ProfessionalService.Create(drCarlos.Id, exameRotina.Id),
            ProfessionalService.Create(drFernanda.Id, exameRotina.Id),
            ProfessionalService.Create(drFernanda.Id, checkUp.Id),
            ProfessionalService.Create(drFernanda.Id, retorno.Id),
        ], ct);

        // ── Availability rules ────────────────────────────────────
        // Dra. Ana: segunda a sexta, 08:00–17:00, slot de 30 min
        foreach (var day in new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday })
        {
            await db.AvailabilityRules.AddAsync(
                AvailabilityRule.Create(tenant.Id, drAna.Id, day,
                    new TimeOnly(8, 0), new TimeOnly(17, 0), slotIntervalMinutes: 30), ct);
        }

        // Dr. Carlos: terça, quinta e sábado, 09:00–13:00, slot de 30 min
        foreach (var day in new[] { DayOfWeek.Tuesday, DayOfWeek.Thursday, DayOfWeek.Saturday })
        {
            await db.AvailabilityRules.AddAsync(
                AvailabilityRule.Create(tenant.Id, drCarlos.Id, day,
                    new TimeOnly(9, 0), new TimeOnly(13, 0), slotIntervalMinutes: 30), ct);
        }

        // Dra. Fernanda: segunda, quarta e sexta, 07:00–12:00 e 14:00–18:00
        foreach (var day in new[] { DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday })
        {
            await db.AvailabilityRules.AddAsync(
                AvailabilityRule.Create(tenant.Id, drFernanda.Id, day,
                    new TimeOnly(7, 0), new TimeOnly(12, 0), slotIntervalMinutes: 30), ct);
            await db.AvailabilityRules.AddAsync(
                AvailabilityRule.Create(tenant.Id, drFernanda.Id, day,
                    new TimeOnly(14, 0), new TimeOnly(18, 0), slotIntervalMinutes: 30), ct);
        }

        // ── Sample customers ──────────────────────────────────────
        await db.Customers.AddRangeAsync([
            Customer.Create(tenant.Id, "Maria Silva", "maria.silva@email.com", "(11) 98000-1001"),
            Customer.Create(tenant.Id, "João Santos", "joao.santos@email.com", "(11) 98000-1002"),
            Customer.Create(tenant.Id, "Fernanda Costa", "fernanda.costa@email.com", "(11) 98000-1003"),
            Customer.Create(tenant.Id, "Pedro Oliveira", "pedro.oliveira@email.com", "(11) 98000-1004"),
            Customer.Create(tenant.Id, "Carla Souza", "carla.souza@email.com", "(11) 98000-1005"),
        ], ct);

        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Development seed complete: tenant '{Slug}', 3 profissionais, 4 serviços, 5 clientes.",
            tenant.Slug);
    }
}
