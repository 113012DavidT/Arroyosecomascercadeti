using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using arroyoSeco.Application.Common.Interfaces;
using arroyoSeco.Domain.Entities.Solicitudes;
using arroyoSeco.Domain.Entities.Usuarios;
using UsuarioOferente = arroyoSeco.Domain.Entities.Usuarios.Oferente;

namespace arroyoSeco.Controllers;

[ApiController]
[Route("api/admin/oferentes")]
[Authorize(Roles = "Admin")] // solo admin
public class OferentesAdminController : ControllerBase
{
    private readonly IAppDbContext _db;
    private readonly INotificationService _noti;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;

    public OferentesAdminController(
        IAppDbContext db,
        INotificationService noti,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager)
    {
        _db = db;
        _noti = noti;
        _userManager = userManager;
        _roleManager = roleManager;
    }

    // Crear usuario Identity de tipo Oferente y su registro en tabla Oferentes
    public record CrearUsuarioOferenteDto(string Email, string Password, string Nombre);

    [HttpPost("usuarios")]
    public async Task<IActionResult> CrearUsuarioOferente([FromBody] CrearUsuarioOferenteDto dto, CancellationToken ct)
    {
        var existing = await _userManager.FindByEmailAsync(dto.Email);
        if (existing is not null) return Conflict("Ya existe un usuario con ese email.");

        var user = new ApplicationUser { UserName = dto.Email, Email = dto.Email, EmailConfirmed = true, RequiereCambioPassword = true };
        var res = await _userManager.CreateAsync(user, dto.Password);
        if (!res.Succeeded) return BadRequest(res.Errors);

        if (!await _roleManager.RoleExistsAsync("Oferente"))
            await _roleManager.CreateAsync(new IdentityRole("Oferente"));
        await _userManager.AddToRoleAsync(user, "Oferente");

        // Crea el Oferente (dominio)
        if (!await _db.Oferentes.AnyAsync(o => o.Id == user.Id, ct))
        {
            var o = new UsuarioOferente { Id = user.Id, Nombre = dto.Nombre, NumeroAlojamientos = 0 };
            _db.Oferentes.Add(o);
            await _db.SaveChangesAsync(ct);
        }

        // Notificaci�n simple
        await _noti.PushAsync(user.Id, "Cuenta de Oferente creada",
            "Tu cuenta de oferente ha sido creada por un administrador.", "Oferente", null, ct);

        return CreatedAtAction(nameof(Get), new { id = user.Id }, new { user.Id, user.Email });
    }

    // CRUD Oferente
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await _db.Oferentes.AsNoTracking().ToListAsync(ct));

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id, CancellationToken ct)
    {
        var o = await _db.Oferentes.Include(x => x.Alojamientos).FirstOrDefaultAsync(x => x.Id == id, ct);
        return o is null ? NotFound() : Ok(o);
    }

    public record ActualizarOferenteDto(string? Nombre, string? Telefono, int? Tipo);

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] ActualizarOferenteDto dto, CancellationToken ct)
    {
        var o = await _db.Oferentes.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (o is null) return NotFound(new { message = "Oferente no encontrado" });

        // Actualizar nombre si viene
        if (!string.IsNullOrWhiteSpace(dto.Nombre))
        {
            o.Nombre = dto.Nombre;
        }

        // Actualizar tipo si viene
        if (dto.Tipo.HasValue)
        {
            o.Tipo = (arroyoSeco.Domain.Entities.Enums.TipoOferente)dto.Tipo.Value;
        }

        // Actualizar teléfono en Identity si viene
        if (dto.Telefono != null)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user != null)
            {
                user.PhoneNumber = dto.Telefono;
                var updateResult = await _userManager.UpdateAsync(user);
                if (!updateResult.Succeeded)
                {
                    return BadRequest(new { message = "Error al actualizar teléfono", errors = updateResult.Errors });
                }
            }
        }

        await _db.SaveChangesAsync(ct);

        // Retornar el oferente actualizado con todos los datos
        var userFinal = await _userManager.FindByIdAsync(id);
        return Ok(new
        {
            o.Id,
            o.Nombre,
            Tipo = (int)o.Tipo,
            o.Estado,
            Email = userFinal?.Email,
            Telefono = userFinal?.PhoneNumber
        });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        var o = await _db.Oferentes.Include(x => x.Alojamientos).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (o is null) return NotFound();
        if (o.Alojamientos?.Any() == true) return BadRequest("No se puede eliminar: tiene alojamientos asociados.");
        _db.Oferentes.Remove(o);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    // Gesti�n de Solicitudes de Oferente (opcional si usas el flujo de solicitudes)
    [HttpGet("solicitudes")]
    public async Task<IActionResult> ListSolicitudes([FromQuery] string? estatus, CancellationToken ct)
    {
        var q = _db.SolicitudesOferente.AsQueryable();
        if (!string.IsNullOrWhiteSpace(estatus)) q = q.Where(s => s.Estatus == estatus);
        var items = await q.OrderByDescending(s => s.FechaSolicitud).AsNoTracking().ToListAsync(ct);
        return Ok(items);
    }

    [HttpPost("solicitudes/{id:int}/aprobar")]
    public async Task<IActionResult> Aprobar(int id, CancellationToken ct)
    {
        var s = await _db.SolicitudesOferente.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (s is null) return NotFound(new { message = "Solicitud no encontrada" });

        // crea (o reutiliza) usuario por correo de la solicitud
        var email = string.IsNullOrWhiteSpace(s.Correo) ? $"oferente{id}@arroyoseco.com" : s.Correo.Trim();
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser 
            { 
                UserName = email, 
                Email = email, 
                EmailConfirmed = true,
                PhoneNumber = s.Telefono,
                RequiereCambioPassword = true
            };
            
            // Generar contraseña temporal
            var tempPass = "Temporal.123"; // O generar: "Temp" + Guid.NewGuid().ToString("N")[..8] + "!";
            var res = await _userManager.CreateAsync(user, tempPass);
            if (!res.Succeeded) return BadRequest(res.Errors);
            
            // Asignar rol Oferente
            if (!await _roleManager.RoleExistsAsync("Oferente"))
                await _roleManager.CreateAsync(new IdentityRole("Oferente"));
            await _userManager.AddToRoleAsync(user, "Oferente");
        }

        // Crear oferente si no existe
        if (!await _db.Oferentes.AnyAsync(o => o.Id == user.Id, ct))
        {
            _db.Oferentes.Add(new UsuarioOferente 
            { 
                Id = user.Id, 
                Nombre = s.NombreNegocio, 
                NumeroAlojamientos = 0,
                Tipo = s.TipoSolicitado, // Usar el tipo solicitado
                Estado = "Pendiente" // Estado inicial
            });
        }

        // Actualizar solicitud
        s.Estatus = "Aprobada";
        s.FechaRespuesta = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        // Notificar al usuario aprobado
        await _noti.PushAsync(user.Id, "Solicitud aprobada",
            $"Tu solicitud para ser oferente de {GetTipoTexto((int)s.TipoSolicitado)} fue aprobada. Contraseña temporal: Temporal.123", 
            "SolicitudOferente", null, ct);

        return Ok(new { id = user.Id, email = user.Email, tipo = s.TipoSolicitado, message = "Solicitud aprobada" });
    }

    private string GetTipoTexto(int tipo) => tipo switch
    {
        1 => "Alojamiento",
        2 => "Gastronomía",
        3 => "Ambos",
        _ => "Desconocido"
    };

    [HttpPost("solicitudes/{id:int}/rechazar")]
    public async Task<IActionResult> Rechazar(int id, CancellationToken ct)
    {
        var s = await _db.SolicitudesOferente.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (s is null) return NotFound(new { message = "Solicitud no encontrada" });
        
        s.Estatus = "Rechazada";
        s.FechaRespuesta = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        
        return Ok(new { message = "Solicitud rechazada" });
    }

    // Cambiar estado de oferente
    public record CambiarEstadoDto(string Estado);

    [HttpPut("{id}/estado")]
    public async Task<IActionResult> CambiarEstado(string id, [FromBody] CambiarEstadoDto dto, CancellationToken ct)
    {
        var oferente = await _db.Oferentes.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (oferente == null) 
            return NotFound(new { message = "Oferente no encontrado" });
        
        oferente.Estado = dto.Estado;
        await _db.SaveChangesAsync(ct);
        
        return Ok(new { 
            id = oferente.Id, 
            estado = oferente.Estado 
        });
    }
}