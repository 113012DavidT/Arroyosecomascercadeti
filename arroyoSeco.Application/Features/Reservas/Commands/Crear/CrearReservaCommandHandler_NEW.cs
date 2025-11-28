using Microsoft.EntityFrameworkCore;
using arroyoSeco.Application.Common.Interfaces;
using arroyoSeco.Domain.Entities.Alojamientos;

namespace arroyoSeco.Application.Features.Reservas.Commands.Crear;

public class CrearReservaCommandHandler
{
    private readonly IAppDbContext _ctx;
    private readonly ICurrentUserService _current;
    private readonly IFolioGenerator _folio;
    private readonly INotificationService _noti;

    public CrearReservaCommandHandler(IAppDbContext ctx, ICurrentUserService current, IFolioGenerator folio, INotificationService noti)
    {
        _ctx = ctx;
        _current = current;
        _folio = folio;
        _noti = noti;
    }

    public async Task<int> Handle(CrearReservaCommand request, CancellationToken ct = default)
    {
        // El frontend envia las fechas ya ajustadas, solo las normalizamos a UTC
        var entrada = DateTime.SpecifyKind(request.FechaEntrada.Date, DateTimeKind.Utc);
        var salida = DateTime.SpecifyKind(request.FechaSalida.Date, DateTimeKind.Utc);

        if (entrada >= salida)
            throw new InvalidOperationException("Rango de fechas invalido");

        // Estado que bloquea disponibilidad (confirmadas o en curso de confirmacion)
        var estadosBloquean = new[] { "Confirmada", "PagoEnRevision" };

        // Solapamiento (entrada < existente.FechaSalida && salida > existente.FechaEntrada)
        var existeOverlap = await _ctx.Reservas
            .AsNoTracking()
            .AnyAsync(r =>
                r.AlojamientoId == request.AlojamientoId &&
                estadosBloquean.Contains(r.Estado) &&
                entrada < r.FechaSalida &&
                salida > r.FechaEntrada, ct);

        if (existeOverlap)
            throw new InvalidOperationException("Fechas no disponibles");

        var alojamiento = await _ctx.Alojamientos
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == request.AlojamientoId, ct);
        if (alojamiento is null)
            throw new InvalidOperationException("Alojamiento inexistente");

        var noches = (salida - entrada).Days;
        if (noches <= 0) throw new InvalidOperationException("Duracion invalida");

        var total = noches * alojamiento.PrecioPorNoche;

        try
        {
            Console.WriteLine($"[CrearReservaCommandHandler] Generando folio...");
            var folio = await _folio.NextReservaFolioAsync(ct);
            Console.WriteLine($"[CrearReservaCommandHandler] Folio generado: {folio}");

            var reserva = new Reserva
            {
                AlojamientoId = request.AlojamientoId,
                ClienteId = _current.UserId,
                FechaEntrada = entrada,
                FechaSalida = salida,
                Total = total,
                Folio = folio,
                Estado = "Pendiente",
                FechaReserva = DateTime.UtcNow
            };

            Console.WriteLine($"[CrearReservaCommandHandler] Agregando reserva a contexto...");
            _ctx.Reservas.Add(reserva);

            Console.WriteLine($"[CrearReservaCommandHandler] Guardando cambios en BD...");
            await _ctx.SaveChangesAsync(ct);
            Console.WriteLine($"[CrearReservaCommandHandler] Reserva guardada. ID={reserva.Id}");

            // Notificacion al oferente (opcional)
            if (!string.IsNullOrWhiteSpace(alojamiento.OferenteId))
            {
                Console.WriteLine($"[CrearReservaCommandHandler] Enviando notificacion al oferente {alojamiento.OferenteId}...");
                await _noti.PushAsync(alojamiento.OferenteId,
                    "Nueva reserva",
                    $"Reserva {reserva.Folio} pendiente de comprobante.",
                    "Reserva",
                    url: $"/reservas/{reserva.Folio}",
                    ct: ct);
                Console.WriteLine($"[CrearReservaCommandHandler] Notificacion enviada");
            }

            return reserva.Id;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CrearReservaCommandHandler] ERROR: {ex.GetType().Name} - {ex.Message}");
            if (ex.InnerException != null)
                Console.WriteLine($"[CrearReservaCommandHandler] INNER: {ex.InnerException.GetType().Name} - {ex.InnerException.Message}");
            Console.WriteLine($"[CrearReservaCommandHandler] STACK: {ex.StackTrace}");
            throw;
        }
    }
}
