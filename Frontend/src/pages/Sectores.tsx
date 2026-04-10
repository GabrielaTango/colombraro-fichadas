import { useState, useMemo } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import Select from 'react-select';
import { sectoresService } from '../services/sectoresService';
import { novedadesService } from '../services/novedadesService';
import Swal from 'sweetalert2';
import type { Sector } from '../types';

export const Sectores = () => {
  const queryClient = useQueryClient();
  const [editingSector, setEditingSector] = useState<Sector | null>(null);
  const [showForm, setShowForm] = useState(false);
  const [novedadTrabajadasSeleccionada, setNovedadTrabajadasSeleccionada] = useState<number | undefined>(undefined);
  const [novedadExtrasSeleccionada, setNovedadExtrasSeleccionada] = useState<number | undefined>(undefined);
  const [novedadErrors, setNovedadErrors] = useState<{ trabajadas?: string; extras?: string }>({});

  const { data: sectores, isLoading } = useQuery({
    queryKey: ['sectores'],
    queryFn: () => sectoresService.getAll(),
  });

  const { data: novedades } = useQuery({
    queryKey: ['novedades'],
    queryFn: () => novedadesService.getAll(),
  });

  // Opciones memoizadas para react-select
  const novedadOptions = useMemo(() => {
    return novedades?.map((novedad) => ({
      value: novedad.idNovedad,
      label: `${novedad.codNovedad} - ${novedad.descNovedad}`,
      novedad: novedad,
    })) || [];
  }, [novedades]);

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<{ nombre: string; esRotativo: boolean }>();

  const createMutation = useMutation({
    mutationFn: (data: { nombre: string; esRotativo: boolean; novedadExtrasId?: number; novedadTrabajadasId?: number }) => sectoresService.create(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['sectores'] });
      reset();
      setNovedadTrabajadasSeleccionada(undefined);
      setNovedadExtrasSeleccionada(undefined);
      setNovedadErrors({});
      setShowForm(false);
      Swal.fire({
        icon: 'success',
        title: 'Sector creado',
        text: 'El sector ha sido creado exitosamente',
        timer: 2000,
        showConfirmButton: false
      });
    },
    onError: () => {
      Swal.fire({
        icon: 'error',
        title: 'Error',
        text: 'No se pudo crear el sector'
      });
    },
  });

  const updateMutation = useMutation({
    mutationFn: ({ id, data }: { id: number; data: { nombre: string; esRotativo: boolean; novedadExtrasId?: number; novedadTrabajadasId?: number } }) =>
      sectoresService.update(id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['sectores'] });
      reset();
      setEditingSector(null);
      setNovedadTrabajadasSeleccionada(undefined);
      setNovedadExtrasSeleccionada(undefined);
      setNovedadErrors({});
      setShowForm(false);
      Swal.fire({
        icon: 'success',
        title: 'Sector actualizado',
        text: 'El sector ha sido actualizado exitosamente',
        timer: 2000,
        showConfirmButton: false
      });
    },
    onError: () => {
      Swal.fire({
        icon: 'error',
        title: 'Error',
        text: 'No se pudo actualizar el sector'
      });
    },
  });

  const deleteMutation = useMutation({
    mutationFn: (id: number) => sectoresService.delete(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['sectores'] });
      Swal.fire({
        icon: 'success',
        title: 'Sector eliminado',
        text: 'El sector ha sido eliminado exitosamente',
        timer: 2000,
        showConfirmButton: false
      });
    },
    onError: () => {
      Swal.fire({
        icon: 'error',
        title: 'Error',
        text: 'No se pudo eliminar el sector'
      });
    },
  });

  const onSubmit = (data: { nombre: string; esRotativo: boolean }) => {
    // Validar novedades obligatorias
    const newErrors: { trabajadas?: string; extras?: string } = {};
    if (!novedadTrabajadasSeleccionada) {
      newErrors.trabajadas = 'La novedad de horas trabajadas es requerida';
    }
    if (!novedadExtrasSeleccionada) {
      newErrors.extras = 'La novedad de horas extras es requerida';
    }
    if (Object.keys(newErrors).length > 0) {
      setNovedadErrors(newErrors);
      return;
    }
    setNovedadErrors({});

    // Usar los valores de los estados para las novedades
    const dataToSend = {
      nombre: data.nombre,
      esRotativo: data.esRotativo,
      novedadExtrasId: novedadExtrasSeleccionada,
      novedadTrabajadasId: novedadTrabajadasSeleccionada,
    };

    if (editingSector) {
      updateMutation.mutate({ id: editingSector.idSector, data: dataToSend });
    } else {
      createMutation.mutate(dataToSend);
    }
  };

  const handleEdit = (sector: Sector) => {
    setEditingSector(sector);
    reset({
      nombre: sector.nombre || '',
      esRotativo: sector.esRotativo || false
    });
    setNovedadTrabajadasSeleccionada(sector.novedadTrabajadasId || undefined);
    setNovedadExtrasSeleccionada(sector.novedadExtrasId || undefined);
    setShowForm(true);
    // Scroll hacia arriba para ver el formulario
    window.scrollTo({ top: 0, behavior: 'smooth' });
  };

  const handleCancelEdit = () => {
    setEditingSector(null);
    reset({
      nombre: '',
      esRotativo: false
    });
    setNovedadTrabajadasSeleccionada(undefined);
    setNovedadExtrasSeleccionada(undefined);
    setNovedadErrors({});
    setShowForm(false);
  };

  const handleDelete = (id: number, nombre: string) => {
    Swal.fire({
      title: '¿Está seguro?',
      text: `¿Desea eliminar el sector "${nombre}"?`,
      icon: 'warning',
      showCancelButton: true,
      confirmButtonColor: '#d33',
      cancelButtonColor: '#3085d6',
      confirmButtonText: 'Sí, eliminar',
      cancelButtonText: 'Cancelar'
    }).then((result) => {
      if (result.isConfirmed) {
        deleteMutation.mutate(id);
      }
    });
  };

  return (
    <div className="container">
      <div className="d-flex justify-content-between align-items-center mb-4">
        <h1>Gestión de Sectores</h1>
        {!showForm && (
          <button
            className="btn btn-primary"
            onClick={() => setShowForm(true)}
          >
            + Nuevo Sector
          </button>
        )}
      </div>

      {showForm && (
        <div className="card mb-4">
          <div className="card-body">
            <form onSubmit={handleSubmit(onSubmit)}>
              <h5 className="card-title">{editingSector ? 'Editar Sector' : 'Nuevo Sector'}</h5>

              <div className="mb-3">
                <label htmlFor="nombre" className="form-label">Nombre del Sector *</label>
                <input
                  id="nombre"
                  type="text"
                  {...register('nombre', { required: 'El nombre es requerido' })}
                  className={`form-control ${errors.nombre ? 'is-invalid' : ''}`}
                />
                {errors.nombre && (
                  <div className="invalid-feedback">{errors.nombre.message}</div>
                )}
              </div>

              <div className="mb-3">
                <div className="form-check">
                  <input
                    id="esRotativo"
                    type="checkbox"
                    {...register('esRotativo')}
                    className="form-check-input"
                  />
                  <label htmlFor="esRotativo" className="form-check-label">
                    Sector con turnos rotativos (día/noche semanales)
                  </label>
                </div>
                <small className="form-text text-muted">
                  Si está marcado, el sector tendrá dos horarios (diurno y nocturno) que rotan semanalmente
                </small>
              </div>

              <div className="mb-3">
                <label htmlFor="novedadTrabajadasId" className="form-label">
                  Novedad para Horas Trabajadas (Importación desde Excel) *
                </label>
                <Select
                  id="novedadTrabajadasId"
                  options={novedadOptions}
                  value={novedadOptions.find(opt => opt.value === novedadTrabajadasSeleccionada) || null}
                  onChange={(option) => {
                    setNovedadTrabajadasSeleccionada(option?.value || undefined);
                    if (option?.value) setNovedadErrors(prev => ({ ...prev, trabajadas: undefined }));
                  }}
                  placeholder="Seleccione una novedad..."
                  isClearable
                  noOptionsMessage={() => 'No se encontraron novedades'}
                  styles={{
                    control: (base) => ({
                      ...base,
                      minHeight: '38px',
                      borderColor: novedadErrors.trabajadas ? '#dc3545' : '#dee2e6',
                    }),
                  }}
                  theme={(theme) => ({
                    ...theme,
                    colors: {
                      ...theme.colors,
                      primary: '#0d6efd',
                      primary25: '#e7f1ff',
                    },
                  })}
                />
                {novedadErrors.trabajadas && (
                  <div className="text-danger small mt-1">{novedadErrors.trabajadas}</div>
                )}
                <small className="form-text text-muted">
                  Novedad que se asignará automáticamente a las fichadas al importar desde Excel.
                </small>
              </div>

              <div className="mb-3">
                <label htmlFor="novedadExtrasId" className="form-label">
                  Novedad para Horas Extras (Exportación a Tango) *
                </label>
                <Select
                  id="novedadExtrasId"
                  options={novedadOptions}
                  value={novedadOptions.find(opt => opt.value === novedadExtrasSeleccionada) || null}
                  onChange={(option) => {
                    setNovedadExtrasSeleccionada(option?.value || undefined);
                    if (option?.value) setNovedadErrors(prev => ({ ...prev, extras: undefined }));
                  }}
                  placeholder="Seleccione una novedad..."
                  isClearable
                  noOptionsMessage={() => 'No se encontraron novedades'}
                  styles={{
                    control: (base) => ({
                      ...base,
                      minHeight: '38px',
                      borderColor: novedadErrors.extras ? '#dc3545' : '#dee2e6',
                    }),
                  }}
                  theme={(theme) => ({
                    ...theme,
                    colors: {
                      ...theme.colors,
                      primary: '#0d6efd',
                      primary25: '#e7f1ff',
                    },
                  })}
                />
                {novedadErrors.extras && (
                  <div className="text-danger small mt-1">{novedadErrors.extras}</div>
                )}
                <small className="form-text text-muted">
                  Novedad que se usará al exportar las horas extras a Tango.
                </small>
              </div>

              <div className="d-flex gap-2">
                <button type="submit" className="btn btn-primary">
                  {editingSector ? 'Actualizar' : 'Guardar'}
                </button>
                <button
                  type="button"
                  className="btn btn-secondary"
                  onClick={handleCancelEdit}
                >
                  Cancelar
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {!showForm && (
        <>
          {isLoading ? (
            <div className="text-center">
              <div className="spinner-border" role="status">
                <span className="visually-hidden">Cargando...</span>
              </div>
            </div>
          ) : (
            <div className="card">
          <div className="card-body">
            <div className="table-responsive">
              <table className="table table-striped table-hover">
                <thead>
                  <tr>
                    <th>ID</th>
                    <th>Nombre</th>
                    <th>Turnos Rotativos</th>
                    <th>Novedad Trabajadas</th>
                    <th>Novedad Extras</th>
                    <th>Acciones</th>
                  </tr>
                </thead>
                <tbody>
                  {sectores?.map((sector) => (
                    <tr key={sector.idSector}>
                      <td>{sector.idSector}</td>
                      <td>{sector.nombre}</td>
                      <td>
                        {sector.esRotativo ? (
                          <span className="badge bg-info">Sí</span>
                        ) : (
                          <span className="badge bg-secondary">No</span>
                        )}
                      </td>
                      <td>
                        {sector.novedadTrabajadasCodigo ? (
                          <span className="text-muted small">
                            {sector.novedadTrabajadasCodigo}
                          </span>
                        ) : (
                          <span className="text-muted small">-</span>
                        )}
                      </td>
                      <td>
                        {sector.novedadExtrasCodigo ? (
                          <span className="text-muted small">
                            {sector.novedadExtrasCodigo}
                          </span>
                        ) : (
                          <span className="text-muted small">-</span>
                        )}
                      </td>
                      <td>
                        <div className="btn-group" role="group">
                          <button
                            className="btn btn-sm btn-outline-primary"
                            onClick={() => handleEdit(sector)}
                          >
                            Editar
                          </button>
                          <button
                            className="btn btn-sm btn-outline-danger"
                            onClick={() =>
                              handleDelete(sector.idSector, sector.nombre || '')
                            }
                          >
                            Eliminar
                          </button>
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>

              {sectores?.length === 0 && (
                <div className="alert alert-info">No se encontraron sectores</div>
              )}
            </div>
          </div>
            </div>
          )}
        </>
      )}
    </div>
  );
};
