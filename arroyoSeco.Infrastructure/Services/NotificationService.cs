using arroyoSeco.Application.Common.Interfaces;
using arroyoSeco.Domain.Entities.Notificaciones;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using arroyoSeco.Domain.Entities.Usuarios;

namespace arroyoSeco.Infrastructure.Services;

public class NotificationService : INotificationService
{
    private readonly IAppDbContext _ctx;
    private readonly IEmailService _email;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        IAppDbContext ctx,
        IEmailService email,
        UserManager<ApplicationUser> userManager,
        ILogger<NotificationService> logger)
    {
        _ctx = ctx;
        _email = email;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<int> PushAsync(
        string usuarioId,
        string titulo,
        string mensaje,
        string tipo,
        string? url = null,
        CancellationToken ct = default)
    {
        var n = new Notificacion
        {
            UsuarioId = usuarioId,
            Titulo = titulo,
            Mensaje = mensaje,
            Tipo = tipo,
            UrlAccion = url,
            Leida = false,
            Fecha = DateTime.UtcNow
        };

        _ctx.Notificaciones.Add(n);
        await _ctx.SaveChangesAsync(ct);

        // Enviar correo de forma asíncrona (sin bloquear)
        _ = Task.Run(async () =>
        {
            try
            {
                var user = await _userManager.FindByIdAsync(usuarioId);
                if (user?.Email != null)
                {
                    await _email.SendNotificationEmailAsync(
                        user.Email,
                        titulo,
                        mensaje,
                        url,
                        ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error enviando email para notificación {n.Id}");
            }
        }, ct);

        return n.Id;
    }

    public async Task MarkAsReadAsync(int id, string usuarioId, CancellationToken ct = default)
    {
        var n = await _ctx.Notificaciones.FindAsync(new object[] { id }, ct);
        if (n is null || n.UsuarioId != usuarioId) return;

        n.Leida = true;
        await _ctx.SaveChangesAsync(ct);
    }
}