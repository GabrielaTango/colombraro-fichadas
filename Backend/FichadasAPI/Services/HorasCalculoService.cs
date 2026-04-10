using FichadasAPI.Models;
using FichadasAPI.Repositories;

namespace FichadasAPI.Services;

public class HorasCalculoService : IHorasCalculoService
{
    private readonly IConfiguracionCalculoRepository _configuracionRepo;
    private readonly IEmpleadoRepository _empleadoRepo;
    private readonly ISectorRepository _sectorRepo;

    public HorasCalculoService(
        IConfiguracionCalculoRepository configuracionRepo,
        IEmpleadoRepository empleadoRepo,
        ISectorRepository sectorRepo)
    {
        _configuracionRepo = configuracionRepo;
        _empleadoRepo = empleadoRepo;
        _sectorRepo = sectorRepo;
    }

    public async Task<ResultadoCalculoHoras> CalcularHorasAsync(
        int empleadoId,
        DateTime horaEntrada,
        DateTime horaSalida)
    {
        var resultado = new ResultadoCalculoHoras();

        // 1. Obtener el empleado y su sector
        var empleado = await _empleadoRepo.GetByIdAsync(empleadoId);
        if (empleado == null)
        {
            resultado.Advertencias.Add($"Empleado con ID {empleadoId} no encontrado");
            return resultado;
        }

        if (empleado.SectorId == null)
        {
            resultado.Advertencias.Add($"Empleado {empleado.Nombre} (Legajo {empleado.Legajo}) no tiene sector asignado");
            return resultado;
        }

        // 2. Obtener el sector para verificar si es rotativo
        var sector = await _sectorRepo.GetByIdAsync(empleado.SectorId.Value);
        if (sector == null)
        {
            resultado.Advertencias.Add($"Sector con ID {empleado.SectorId.Value} no encontrado");
            return resultado;
        }

        // 3. Determinar configuración según si el sector es rotativo o no
        ConfiguracionCalculo? configuracion = null;

        if (sector.EsRotativo)
        {
            // Sector rotativo: calcular qué turno le corresponde
            if (empleado.FechaInicioRotacion == null)
            {
                resultado.Advertencias.Add($"Empleado {empleado.Nombre} pertenece a un sector rotativo pero no tiene fecha de inicio de rotación configurada");
                return resultado;
            }

            // Calcular el turno según la fecha de la fichada
            decimal diasDesdeInicio = (horaEntrada.Date - empleado.FechaInicioRotacion.Value.Date).Days;

            decimal semanasCompletas = Math.Ceiling(diasDesdeInicio / 7);

            if (diasDesdeInicio % 7 == 0)
                semanasCompletas++;

            // Si el número de semanas es impar → turno diurno, si es par → turno nocturno
            var tipoTurno = (semanasCompletas % 2 != 0) ? "diurno" : "nocturno";

            // Obtener la configuración del turno correspondiente
            configuracion = await _configuracionRepo.GetBySectorTipoTurnoAsync(empleado.SectorId.Value, tipoTurno);

            if (configuracion == null)
            {
                resultado.Advertencias.Add($"No se encontró configuración activa para el sector rotativo {sector.Nombre} - turno {tipoTurno}. Configure las reglas de cálculo para ambos turnos.");
                return resultado;
            }

            // Validar que la hora de entrada corresponda con el tipo de turno
            var horaDelDia = horaEntrada.TimeOfDay;
            var esTurnoNocturnoReal = horaDelDia.Hours >= 12; // Después de 12 PM es nocturno
            var tipoTurnoEsperado = esTurnoNocturnoReal ? "nocturno" : "diurno";

            if (tipoTurnoEsperado != tipoTurno)
            {
                resultado.Advertencias.Add($"❌ ADVERTENCIA: La hora de entrada ({horaEntrada:HH:mm}) sugiere turno {tipoTurnoEsperado}, pero según la rotación corresponde turno {tipoTurno}. Verificar fecha de inicio de rotación o datos de la fichada.");
                return resultado;
            }
        }
        else
        {
            // Sector no rotativo: obtener la configuración activa del sector
            configuracion = await _configuracionRepo.GetActivaBySectorAsync(empleado.SectorId.Value);

            if (configuracion == null)
            {
                resultado.Advertencias.Add($"No se encontró configuración activa para el sector {empleado.SectorId}. Configure las reglas de cálculo para este sector.");
                return resultado;
            }
        }

        resultado.ConfiguracionUtilizada = configuracion;

        // 3. Ajustar horas según tipo de turno
        DateTime horaEntradaAjustada = horaEntrada;
        DateTime horaSalidaAjustada = horaSalida;

        configuracion.TipoTurno = configuracion.TipoTurno == null ? "diurno" : configuracion.TipoTurno = configuracion.TipoTurno;


        // Para turno DIURNO: si entra antes del horario esperado, no cuenta como extra
        if (configuracion.TipoTurno == "diurno" && configuracion.HoraEntradaEsperada.HasValue)
        {
            var horaEntradaEsperadaDt = horaEntrada.Date.Add(configuracion.HoraEntradaEsperada.Value);
            if (horaEntrada < horaEntradaEsperadaDt)
            {
                var minutosAnticipados = (int)(horaEntradaEsperadaDt - horaEntrada).TotalMinutes;
                horaEntradaAjustada = horaEntradaEsperadaDt;
                resultado.Advertencias.Add($"Turno diurno: entrada anticipada {minutosAnticipados} min. No cuenta como extra.");
            }
        }

        // Para turno NOCTURNO: si sale después del horario esperado, no cuenta como extra
        if (configuracion.TipoTurno == "nocturno" && configuracion.HoraSalidaEsperada.HasValue)
        {
            // La hora de salida esperada es al día siguiente para turnos nocturnos
            var horaSalidaEsperadaDt = horaEntrada.Date.AddDays(1).Add(configuracion.HoraSalidaEsperada.Value);

            // Si la salida esperada es mayor a 12 horas, probablemente sea el mismo día
            if ((horaSalidaEsperadaDt - horaEntrada).TotalHours > 16)
            {
                horaSalidaEsperadaDt = horaEntrada.Date.Add(configuracion.HoraSalidaEsperada.Value);
            }

            if (horaSalida > horaSalidaEsperadaDt)
            {
                var minutosExcedidos = (int)(horaSalida - horaSalidaEsperadaDt).TotalMinutes;
                horaSalidaAjustada = horaSalidaEsperadaDt;
                resultado.Advertencias.Add($"Turno nocturno: salida excedida {minutosExcedidos} min. No cuenta como extra.");
            }
        }

        // 4. Calcular total de minutos trabajados (con horas ajustadas)
        var totalMinutos = (int)(horaSalidaAjustada - horaEntradaAjustada).TotalMinutes;
        resultado.HorasTotales = totalMinutos; // (int)(horaSalida - horaEntrada).TotalMinutes; // Total real para mostrar

        // 5. Calcular llegada tarde (si hay hora de entrada esperada)
        int minutosDescuento = 0;
        int minutosTarde = 0;

        if (configuracion.HoraEntradaEsperada.HasValue)
        {
            var horaEntradaEsperadaDt = horaEntrada.Date.Add(configuracion.HoraEntradaEsperada.Value);

            // Si llegó tarde
            if (horaEntrada > horaEntradaEsperadaDt)
            {
                minutosTarde = (int)(horaEntrada - horaEntradaEsperadaDt).TotalMinutes;

                // Aplicar tolerancia
                if (minutosTarde > configuracion.ToleranciaMinutos)
                {
                    var minutosRealesTarde = minutosTarde - configuracion.ToleranciaMinutos;

                    // Determinar descuento según los rangos
                    if (minutosRealesTarde >= 31)
                    {
                        minutosDescuento = configuracion.DescuentoTarde31Mas;
                    }
                    else if (minutosRealesTarde >= 6)
                    {
                        minutosDescuento = configuracion.DescuentoTarde6a30Min;
                    }

                    if (minutosDescuento > 0)
                    {
                        resultado.Advertencias.Add($"Llegada tarde: {minutosTarde} minutos. Descuento aplicado: {minutosDescuento} minutos");
                    }
                }
            }
        }

        resultado.MinutosTarde = minutosTarde;
        resultado.MinutosDescuento = minutosDescuento;

        // 6. Calcular minutos efectivos trabajados (total - descuento)
        int minutosEfectivos = totalMinutos - minutosDescuento;

        if (minutosEfectivos < 0)
        {
            minutosEfectivos = 0;
            resultado.Advertencias.Add("Los descuentos superan el tiempo trabajado");
        }

        // 7. Distribuir en horas normales, extras oficiales y adicionales
        int horasNormalesMinutos = configuracion.HorasNormales * 60;
        int horasExtrasOficialesMinutos = configuracion.HorasExtrasOficiales * 60;
        int horasExtrasAdicionalesMinutos = configuracion.HorasExtrasAdicionales * 60;

        // Asignar horas normales (hasta el límite configurado)
        if (minutosEfectivos <= horasNormalesMinutos)
        {
            resultado.HorasTrabajadas = minutosEfectivos;
            resultado.HorasExtras = 0;
            resultado.HorasAdicionales = 0;
        }
        else
        {
            resultado.HorasTrabajadas = horasNormalesMinutos;
            int minutosRestantes = minutosEfectivos - horasNormalesMinutos;

            // Asignar horas extras oficiales (hasta el límite configurado)
            if (minutosRestantes <= horasExtrasOficialesMinutos)
            {
                resultado.HorasExtras = minutosRestantes;
                resultado.HorasAdicionales = 0;
            }
            else
            {
                resultado.HorasExtras = horasExtrasOficialesMinutos;
                minutosRestantes -= horasExtrasOficialesMinutos;

                // Asignar horas adicionales (hasta el límite configurado)
                if (horasExtrasAdicionalesMinutos > 0)
                {
                    if (minutosRestantes <= horasExtrasAdicionalesMinutos)
                    {
                        resultado.HorasAdicionales = minutosRestantes;
                    }
                    else
                    {
                        resultado.HorasAdicionales = horasExtrasAdicionalesMinutos;
                        int minutosExcedentes = minutosRestantes - horasExtrasAdicionalesMinutos;
                        resultado.Advertencias.Add($"Tiempo excedente de {minutosExcedentes} minutos no categorizado");
                    }
                }
                else
                {
                    // No hay horas adicionales permitidas para este sector/temporada
                    resultado.Advertencias.Add($"Este sector/temporada no permite horas adicionales. {minutosRestantes} minutos no categorizados");
                }
            }
        }

        // 8. Redondear a horas completas usando la tolerancia configurada
        int tolerancia = configuracion.ToleranciaMinutos;
        resultado.HorasTrabajadas = RedondearAHoraCompleta(resultado.HorasTrabajadas, tolerancia);
        resultado.HorasExtras = RedondearAHoraCompleta(resultado.HorasExtras, tolerancia);
        resultado.HorasAdicionales = RedondearAHoraCompleta(resultado.HorasAdicionales, tolerancia);
        resultado.HorasTotales = resultado.HorasTrabajadas + resultado.HorasExtras + resultado.HorasAdicionales;

        // 9. Advertencias adicionales
        if (totalMinutos > 720) // Más de 12 horas
        {
            resultado.Advertencias.Add("Jornada excede 12 horas. Verificar datos");
        }

        if (totalMinutos < 60) // Menos de 1 hora
        {
            resultado.Advertencias.Add("Jornada menor a 1 hora. Verificar datos");
        }

        return resultado;
    }

    /// <summary>
    /// Redondea los minutos a hora completa (múltiplo de 60) usando la tolerancia.
    /// Si los minutos sobrantes >= tolerancia, redondea hacia arriba; si no, hacia abajo.
    /// Ejemplo con tolerancia de 30: 570 min (9h 30min) → 600 min (10h), 555 min (9h 15min) → 540 min (9h)
    /// </summary>
    private int RedondearAHoraCompleta(int minutos, int tolerancia)
    {
        if (minutos <= 0) return 0;

        int horasCompletas = minutos / 60;
        int minutosSobrantes = minutos % 60;

        // Si los minutos sobrantes son >= tolerancia, redondear hacia arriba
        if (minutosSobrantes >= tolerancia)
        {
            return (horasCompletas + 1) * 60;
        }

        // Si no, redondear hacia abajo
        return horasCompletas * 60;
    }
}
