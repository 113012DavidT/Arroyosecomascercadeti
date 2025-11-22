using Microsoft.EntityFrameworkCore;
using arroyoSeco.Domain.Entities.Alojamientos;
using arroyoSeco.Domain.Entities.Notificaciones;
using arroyoSeco.Domain.Entities.Solicitudes;
using arroyoSeco.Domain.Entities.Gastronomia;
using arroyoSeco.Application.Common.Interfaces;
// Alias para desambiguar el Oferente correcto (de Usuarios)
using UsuarioOferente = arroyoSeco.Domain.Entities.Usuarios.Oferente;

namespace arroyoSeco.Infrastructure.Data;

public class AppDbContext : DbContext, IAppDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // Implementación que coincide EXACTAMENTE con la interfaz (mismo tipo genérico)
    public DbSet<UsuarioOferente> Oferentes => Set<UsuarioOferente>();
    public DbSet<Alojamiento> Alojamientos => Set<Alojamiento>();
    public DbSet<FotoAlojamiento> FotosAlojamiento => Set<FotoAlojamiento>();
    public DbSet<Reserva> Reservas => Set<Reserva>();
    public DbSet<Establecimiento> Establecimientos => Set<Establecimiento>();
    public DbSet<Menu> Menus => Set<Menu>();
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();
    public DbSet<Mesa> Mesas => Set<Mesa>();
    public DbSet<ReservaGastronomia> ReservasGastronomia => Set<ReservaGastronomia>();
    public DbSet<Notificacion> Notificaciones => Set<Notificacion>();
    public DbSet<SolicitudOferente> SolicitudesOferente => Set<SolicitudOferente>();

    public new Task<int> SaveChangesAsync(CancellationToken ct = default) => base.SaveChangesAsync(ct);

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Alojamiento>()
            .HasMany(a => a.Fotos)
            .WithOne(f => f.Alojamiento)
            .HasForeignKey(f => f.AlojamientoId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<Alojamiento>()
            .HasMany(a => a.Reservas)
            .WithOne(r => r.Alojamiento)
            .HasForeignKey(r => r.AlojamientoId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<Alojamiento>()
            .HasOne(a => a.Oferente)
            .WithMany(o => o.Alojamientos)
            .HasForeignKey(a => a.OferenteId)
            .OnDelete(DeleteBehavior.Restrict);

        b.Entity<Establecimiento>()
            .HasOne(e => e.Oferente)
            .WithMany(o => o.Establecimientos)
            .HasForeignKey(e => e.OferenteId)
            .OnDelete(DeleteBehavior.Restrict);

        b.Entity<Establecimiento>()
            .HasMany(e => e.Menus)
            .WithOne(m => m.Establecimiento)
            .HasForeignKey(m => m.EstablecimientoId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<Establecimiento>()
            .HasMany(e => e.Mesas)
            .WithOne(ms => ms.Establecimiento)
            .HasForeignKey(ms => ms.EstablecimientoId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<Establecimiento>()
            .HasMany(e => e.Reservas)
            .WithOne(r => r.Establecimiento)
            .HasForeignKey(r => r.EstablecimientoId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<Menu>()
            .HasMany(m => m.Items)
            .WithOne(i => i.Menu)
            .HasForeignKey(i => i.MenuId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<Reserva>(e =>
        {
            e.HasIndex(r => r.Folio).IsUnique();
            e.Property(r => r.Total).HasColumnType("decimal(65,30)");
            e.Property(r => r.ComprobanteUrl).HasMaxLength(500);
        });

        b.Entity<Notificacion>().HasIndex(n => n.UsuarioId);
        b.Entity<SolicitudOferente>().HasIndex(s => s.Estatus);

        base.OnModelCreating(b);
    }
}