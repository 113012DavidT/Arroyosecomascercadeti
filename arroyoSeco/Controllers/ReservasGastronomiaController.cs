using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using arroyoSeco.Application.Common.Interfaces;

namespace arroyoSeco.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReservasGastronomiaController : ControllerBase
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _current;

    public ReservasGastronomiaController(IAppDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    // GET /api/ReservasGastronomia/activas
    [HttpGet("activas")]
    public async Task<ActionResult> GetReservasActivas(CancellationToken ct)
    {
        var userId = _current.UserId;
        var now = DateTime.UtcNow;

        // Si es cliente, obtener sus reservas activas
        if (User.IsInRole("Cliente"))
        {
            var reservas = await _db.ReservasGastronomia
                .Where(r => r.UsuarioId == userId && 
                           (r.Estado == "Pendiente" || r.Estado == "Confirmada") &&
                           r.Fecha >= now)
                .Include(r => r.Establecimiento)
                .Include(r => r.Mesa)
                .AsNoTracking()
                .OrderBy(r => r.Fecha)
                .ToListAsync(ct);

            return Ok(reservas.Select(r => new
            {
                r.Id,
                r.EstablecimientoId,
                EstablecimientoNombre = r.Establecimiento?.Nombre,
                r.MesaId,
                MesaNumero = r.Mesa?.Numero,
                r.UsuarioId,
                r.Fecha,
                r.NumeroPersonas,
                r.Estado,
                r.Total
            }));
        }

        // Si es oferente, obtener reservas activas de sus establecimientos
        if (User.IsInRole("Oferente"))
        {
            var reservas = await _db.ReservasGastronomia
                .Where(r => r.Establecimiento!.OferenteId == userId &&
                           (r.Estado == "Pendiente" || r.Estado == "Confirmada") &&
                           r.Fecha >= now)
                .Include(r => r.Establecimiento)
                .Include(r => r.Mesa)
                .AsNoTracking()
                .OrderBy(r => r.Fecha)
                .ToListAsync(ct);

            return Ok(reservas.Select(r => new
            {
                r.Id,
                r.EstablecimientoId,
                EstablecimientoNombre = r.Establecimiento?.Nombre,
                r.MesaId,
                MesaNumero = r.Mesa?.Numero,
                r.UsuarioId,
                r.Fecha,
                r.NumeroPersonas,
                r.Estado,
                r.Total
            }));
        }

        return Ok(Array.Empty<object>());
    }

    // GET /api/ReservasGastronomia/historial
    [HttpGet("historial")]
    public async Task<ActionResult> GetHistorial(CancellationToken ct)
    {
        var userId = _current.UserId;
        var now = DateTime.UtcNow;

        if (User.IsInRole("Cliente"))
        {
            var reservas = await _db.ReservasGastronomia
                .Where(r => r.UsuarioId == userId && 
                           (r.Fecha < now || r.Estado == "Cancelada" || r.Estado == "Completada"))
                .Include(r => r.Establecimiento)
                .Include(r => r.Mesa)
                .AsNoTracking()
                .OrderByDescending(r => r.Fecha)
                .ToListAsync(ct);

            return Ok(reservas.Select(r => new
            {
                r.Id,
                r.EstablecimientoId,
                EstablecimientoNombre = r.Establecimiento?.Nombre,
                r.MesaId,
                MesaNumero = r.Mesa?.Numero,
                r.UsuarioId,
                r.Fecha,
                r.NumeroPersonas,
                r.Estado,
                r.Total
            }));
        }

        if (User.IsInRole("Oferente"))
        {
            var reservas = await _db.ReservasGastronomia
                .Where(r => r.Establecimiento!.OferenteId == userId &&
                           (r.Fecha < now || r.Estado == "Cancelada" || r.Estado == "Completada"))
                .Include(r => r.Establecimiento)
                .Include(r => r.Mesa)
                .AsNoTracking()
                .OrderByDescending(r => r.Fecha)
                .ToListAsync(ct);

            return Ok(reservas.Select(r => new
            {
                r.Id,
                r.EstablecimientoId,
                EstablecimientoNombre = r.Establecimiento?.Nombre,
                r.MesaId,
                MesaNumero = r.Mesa?.Numero,
                r.UsuarioId,
                r.Fecha,
                r.NumeroPersonas,
                r.Estado,
                r.Total
            }));
        }

        return Ok(Array.Empty<object>());
    }

    // PATCH /api/ReservasGastronomia/{id}/estado
    [Authorize(Roles = "Admin,Oferente")]
    [HttpPatch("{id:int}/estado")]
    public async Task<IActionResult> CambiarEstado(int id, [FromBody] CambiarEstadoReservaGastronomiaDto dto, CancellationToken ct)
    {
        var reserva = await _db.ReservasGastronomia
            .Include(r => r.Establecimiento)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

        if (reserva == null) return NotFound(new { message = "Reserva no encontrada" });

        // Verificar que el oferente sea dueño del establecimiento
        if (User.IsInRole("Oferente") && reserva.Establecimiento?.OferenteId != _current.UserId)
        {
            return Forbid();
        }

        reserva.Estado = dto.Estado;
        await _db.SaveChangesAsync(ct);

        return Ok(new { reserva.Id, reserva.Estado });
    }

    public record CambiarEstadoReservaGastronomiaDto(string Estado);
}
