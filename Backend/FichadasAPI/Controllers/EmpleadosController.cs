using FichadasAPI.Models;
using FichadasAPI.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FichadasAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class EmpleadosController : ControllerBase
{
    private readonly IEmpleadoRepository _empleadoRepository;
    private readonly ISectorRepository _sectorRepository;
    private readonly IConfiguracionCalculoRepository _configuracionRepository;

    public EmpleadosController(
        IEmpleadoRepository empleadoRepository,
        ISectorRepository sectorRepository,
        IConfiguracionCalculoRepository configuracionRepository)
    {
        _empleadoRepository = empleadoRepository;
        _sectorRepository = sectorRepository;
        _configuracionRepository = configuracionRepository;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Empleado>>> GetAll()
    {
        var empleados = await _empleadoRepository.GetAllAsync();
        return Ok(empleados);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Empleado>> GetById(int id)
    {
        var empleado = await _empleadoRepository.GetByIdAsync(id);
        if (empleado == null)
            return NotFound();

        return Ok(empleado);
    }

    [HttpGet("legajo/{legajo}")]
    public async Task<ActionResult<Empleado>> GetByLegajo(int legajo)
    {
        var empleado = await _empleadoRepository.GetByLegajoAsync(legajo);
        if (empleado == null)
            return NotFound();

        return Ok(empleado);
    }

    [HttpGet("sector/{sectorId}")]
    public async Task<ActionResult<IEnumerable<Empleado>>> GetBySector(int sectorId)
    {
        var empleados = await _empleadoRepository.GetBySectorAsync(sectorId);
        return Ok(empleados);
    }

    /// <summary>
    /// Sugiere el horario de entrada/salida para un empleado en una fecha dada,
    /// basándose en la configuración activa de su sector (y turno rotativo si corresponde).
    /// </summary>
    [HttpGet("{id}/horario-sugerido")]
    public async Task<ActionResult> GetHorarioSugerido(int id, [FromQuery] DateTime fecha)
    {
        var empleado = await _empleadoRepository.GetByIdAsync(id);
        if (empleado == null)
            return NotFound(new { message = "Empleado no encontrado" });

        if (empleado.SectorId == null)
            return Ok(new { horaEntrada = (string?)null, horaSalida = (string?)null, motivo = "Empleado sin sector asignado" });

        var sector = await _sectorRepository.GetByIdAsync(empleado.SectorId.Value);
        if (sector == null)
            return Ok(new { horaEntrada = (string?)null, horaSalida = (string?)null, motivo = "Sector no encontrado" });

        ConfiguracionCalculo? configuracion = null;

        if (sector.EsRotativo)
        {
            if (empleado.FechaInicioRotacion == null)
                return Ok(new { horaEntrada = (string?)null, horaSalida = (string?)null, motivo = "Empleado de sector rotativo sin fecha de inicio de rotación" });

            // Misma lógica que HorasCalculoService para determinar el turno
            decimal diasDesdeInicio = (fecha.Date - empleado.FechaInicioRotacion.Value.Date).Days;
            decimal semanasCompletas = Math.Ceiling(diasDesdeInicio / 7);
            if (diasDesdeInicio % 7 == 0)
                semanasCompletas++;

            var tipoTurno = (semanasCompletas % 2 != 0) ? "diurno" : "nocturno";
            configuracion = await _configuracionRepository.GetBySectorTipoTurnoAsync(empleado.SectorId.Value, tipoTurno);
        }
        else
        {
            configuracion = await _configuracionRepository.GetActivaBySectorAsync(empleado.SectorId.Value);
        }

        if (configuracion == null)
            return Ok(new { horaEntrada = (string?)null, horaSalida = (string?)null, motivo = "No hay configuración activa para el sector" });

        string? horaEntrada = configuracion.HoraEntradaEsperada?.ToString(@"hh\:mm");
        string? horaSalida = configuracion.HoraSalidaEsperada?.ToString(@"hh\:mm");

        return Ok(new { horaEntrada, horaSalida, tipoTurno = configuracion.TipoTurno });
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<int>> Create([FromBody] Empleado empleado)
    {
        var id = await _empleadoRepository.CreateAsync(empleado);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> Update(int id, [FromBody] Empleado empleado)
    {
        empleado.IdEmpleado = id;
        var success = await _empleadoRepository.UpdateAsync(empleado);
        if (!success)
            return NotFound();

        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> Delete(int id)
    {
        var success = await _empleadoRepository.DeleteAsync(id);
        if (!success)
            return NotFound();

        return NoContent();
    }

    /// <summary>
    /// Importa empleados desde la base de datos de Tango (DELTA3.DBO.LEGAJO)
    /// Los empleados se crean sin sector asignado (sector_id = NULL)
    /// </summary>
    [HttpPost("importar-desde-tango")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> ImportarDesdeTango()
    {
        try
        {
            var resultado = await _empleadoRepository.ImportarDesdeTangoAsync();

            if (resultado.TieneErrores)
            {
                return Ok(new
                {
                    message = $"Importación completada con advertencias. Nuevos: {resultado.EmpleadosImportados}, Actualizados: {resultado.EmpleadosActualizados}, Existentes: {resultado.EmpleadosExistentes}",
                    empleadosImportados = resultado.EmpleadosImportados,
                    empleadosActualizados = resultado.EmpleadosActualizados,
                    empleadosExistentes = resultado.EmpleadosExistentes,
                    errores = resultado.Errores
                });
            }

            return Ok(new
            {
                message = $"Importación exitosa. Nuevos: {resultado.EmpleadosImportados}, Actualizados: {resultado.EmpleadosActualizados}, Existentes: {resultado.EmpleadosExistentes}",
                empleadosImportados = resultado.EmpleadosImportados,
                empleadosActualizados = resultado.EmpleadosActualizados,
                empleadosExistentes = resultado.EmpleadosExistentes
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error al importar empleados desde Tango", error = ex.Message });
        }
    }
}
