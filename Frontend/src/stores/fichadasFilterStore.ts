import { create } from 'zustand';

// Funciones para obtener la semana actual
const getLunesSemana = (): string => {
  const today = new Date();
  const day = today.getDay();
  const diff = day === 0 ? -6 : 1 - day;
  const lunes = new Date(today);
  lunes.setDate(today.getDate() + diff);
  return lunes.toISOString().split('T')[0];
};

const getDomingoSemana = (): string => {
  const today = new Date();
  const day = today.getDay();
  const diff = day === 0 ? 0 : 7 - day;
  const domingo = new Date(today);
  domingo.setDate(today.getDate() + diff);
  return domingo.toISOString().split('T')[0];
};

interface FichadasFilterState {
  // Valores de los inputs
  fechaDesdeInput: string;
  fechaHastaInput: string;
  busquedaEmpleadoInput: string;
  exportadaInput: boolean | undefined;

  // Valores aplicados (usados para la query)
  fechaDesdeAplicada: string;
  fechaHastaAplicada: string;
  busquedaEmpleadoAplicada: string;
  exportadaAplicada: boolean | undefined;

  // Acciones
  setFechaDesdeInput: (value: string) => void;
  setFechaHastaInput: (value: string) => void;
  setBusquedaEmpleadoInput: (value: string) => void;
  setExportadaInput: (value: boolean | undefined) => void;
  aplicarFiltros: () => void;
  limpiarFiltros: () => void;
}

export const useFichadasFilterStore = create<FichadasFilterState>((set, get) => {
  const semanaInicio = getLunesSemana();
  const semanaFin = getDomingoSemana();

  return {
    fechaDesdeInput: semanaInicio,
    fechaHastaInput: semanaFin,
    busquedaEmpleadoInput: '',
    exportadaInput: undefined,

    fechaDesdeAplicada: semanaInicio,
    fechaHastaAplicada: semanaFin,
    busquedaEmpleadoAplicada: '',
    exportadaAplicada: undefined,

    setFechaDesdeInput: (value) => set({ fechaDesdeInput: value }),
    setFechaHastaInput: (value) => set({ fechaHastaInput: value }),
    setBusquedaEmpleadoInput: (value) => set({ busquedaEmpleadoInput: value }),
    setExportadaInput: (value) => set({ exportadaInput: value }),

    aplicarFiltros: () => {
      const state = get();
      set({
        fechaDesdeAplicada: state.fechaDesdeInput,
        fechaHastaAplicada: state.fechaHastaInput,
        busquedaEmpleadoAplicada: state.busquedaEmpleadoInput,
        exportadaAplicada: state.exportadaInput,
      });
    },

    limpiarFiltros: () => {
      const semanaInicio = getLunesSemana();
      const semanaFin = getDomingoSemana();
      set({
        fechaDesdeInput: semanaInicio,
        fechaHastaInput: semanaFin,
        busquedaEmpleadoInput: '',
        exportadaInput: undefined,
        fechaDesdeAplicada: semanaInicio,
        fechaHastaAplicada: semanaFin,
        busquedaEmpleadoAplicada: '',
        exportadaAplicada: undefined,
      });
    },
  };
});
