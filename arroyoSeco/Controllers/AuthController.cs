using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using arroyoSeco.Application.Common.Interfaces;
using arroyoSeco.Domain.Entities.Usuarios;

namespace arroyoSeco.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly IJwtTokenGenerator _token;
    private readonly IAppDbContext _db;

    public AuthController(
        UserManager<IdentityUser> userManager,
        SignInManager<IdentityUser> signInManager,
        IJwtTokenGenerator token,
        IAppDbContext db)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _token = token;
        _db = db;
    }

    public record RegisterDto(string Email, string Password, string? Role, int? TipoOferente);
    public record LoginDto(string Email, string Password);

    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto)
    {
        var user = new IdentityUser { UserName = dto.Email, Email = dto.Email, EmailConfirmed = true };
        var result = await _userManager.CreateAsync(user, dto.Password);
        if (!result.Succeeded) return BadRequest(result.Errors);

        // Asignar por defecto rol Cliente (antes era Oferente)
        var role = string.IsNullOrWhiteSpace(dto.Role) ? "Cliente" : dto.Role!;
        await _userManager.AddToRoleAsync(user, role);

        // Crear entidad Oferente si el rol es Oferente
        if (role == "Oferente")
        {
            var tipoOferente = dto.TipoOferente.HasValue 
                ? (Domain.Entities.Enums.TipoOferente)dto.TipoOferente.Value 
                : Domain.Entities.Enums.TipoOferente.Ambos;

            var oferente = new Oferente
            {
                Id = user.Id,
                Nombre = dto.Email.Split('@')[0],
                NumeroAlojamientos = 0,
                Tipo = tipoOferente
            };
            _db.Oferentes.Add(oferente);
            await _db.SaveChangesAsync();
        }

        var roles = await _userManager.GetRolesAsync(user);
        var jwt = _token.Generate(user.Id, user.Email!, roles);
        return Ok(new { token = jwt });
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email);
        if (user is null) return Unauthorized();

        var result = await _signInManager.CheckPasswordSignInAsync(user, dto.Password, lockoutOnFailure: false);
        if (!result.Succeeded) return Unauthorized();

        var roles = await _userManager.GetRolesAsync(user);
        var jwt = _token.Generate(user.Id, user.Email!, roles);
        return Ok(new { token = jwt });
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized();
        var roles = await _userManager.GetRolesAsync(user);
        return Ok(new { id = user.Id, email = user.Email, roles });
    }
}