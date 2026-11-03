import { HttpErrorResponse } from '@angular/common/http';
import { IdKind } from './api';

const IDENTIFIER_PROBLEMS: Record<IdKind, Record<string, string>> = {
  rnc: {
    length: 'Un RNC tiene 9 dígitos.',
    notdigits: 'Solo puede tener dígitos, guiones y espacios.',
    checkdigit: 'El dígito verificador no coincide con los otros ocho.',
    zeros: 'Un cuerpo en ceros no corresponde a ningún RNC.',
  },
  cedula: {
    length: 'Una cédula tiene 11 dígitos.',
    notdigits: 'Solo puede tener dígitos, guiones y espacios.',
    checkdigit: 'El dígito verificador no coincide con los otros diez.',
    zeros: 'Los tres primeros dígitos (000) no son un municipio.',
  },
  ncf: {
    length: 'Un NCF de la serie B tiene 11 caracteres y un e-CF de la serie E tiene 13.',
    series: 'Tiene que empezar con B (comprobante tradicional) o con E (electrónico).',
    not_digits: 'Después de la serie solo puede haber dígitos.',
    type: 'Ese tipo de comprobante no existe en esa serie.',
    sequence: 'La secuencia no puede ser cero.',
  },
};

export function describeProblem(kind: IdKind, problem: string | null | undefined): string {
  if (!problem) return 'No es válido.';
  return IDENTIFIER_PROBLEMS[kind][problem] ?? 'No es válido.';
}

const RANGE_ERRORS: Record<string, string> = {
  kind: 'La serie tiene que ser tradicional o electrónica.',
  type: 'Ese tipo de comprobante no existe en esa serie.',
  bounds:
    'El rango tiene que empezar en 1 o más, terminar después del inicio y caber en los dígitos de la secuencia.',
  expired: 'La fecha de vencimiento ya pasó.',
  overlap: 'Se solapa con otro rango del mismo tipo.',
};

/** Traduce lo que respondió la API a una frase para quien está usando la pantalla. */
export function describeError(e: unknown): string {
  if (!(e instanceof HttpErrorResponse)) return 'Algo falló. Intenta de nuevo.';
  const code = (e.error as { error?: string } | null)?.error;
  switch (e.status) {
    case 0:
      return 'No se pudo conectar con la API.';
    case 400:
      return (code && RANGE_ERRORS[code]) || 'La API rechazó los datos.';
    case 401:
      return 'La clave de API falta o no es correcta.';
    case 404:
      return 'Ese rango no existe.';
    case 409:
      return code === 'exhausted'
        ? 'El rango se agotó: no quedan números por entregar.'
        : (code && RANGE_ERRORS[code]) || 'Hay un conflicto con otro rango.';
    case 410:
      return 'El rango venció y ya no entrega números.';
    case 429:
      return 'Demasiadas peticiones seguidas. Espera un minuto.';
    default:
      return `La API respondió con un error (${e.status}).`;
  }
}

// Listas y no objetos: las claves '11'..'17' de un objeto se ordenarían antes que '01'..'04'.
export const TRADITIONAL_TYPES: { code: string; name: string }[] = [
  { code: '01', name: 'Factura de crédito fiscal' },
  { code: '02', name: 'Factura de consumo' },
  { code: '03', name: 'Nota de débito' },
  { code: '04', name: 'Nota de crédito' },
  { code: '11', name: 'Comprobante de compras' },
  { code: '12', name: 'Registro único de ingresos' },
  { code: '13', name: 'Gastos menores' },
  { code: '14', name: 'Regímenes especiales' },
  { code: '15', name: 'Gubernamental' },
  { code: '16', name: 'Exportaciones' },
  { code: '17', name: 'Pagos al exterior' },
];

export const ELECTRONIC_TYPES: { code: string; name: string }[] = [
  { code: '31', name: 'Factura de crédito fiscal electrónica' },
  { code: '32', name: 'Factura de consumo electrónica' },
  { code: '33', name: 'Nota de débito electrónica' },
  { code: '34', name: 'Nota de crédito electrónica' },
  { code: '41', name: 'Compras electrónico' },
  { code: '43', name: 'Gastos menores electrónico' },
  { code: '44', name: 'Regímenes especiales electrónico' },
  { code: '45', name: 'Gubernamental electrónico' },
  { code: '46', name: 'Exportaciones electrónico' },
  { code: '47', name: 'Pagos al exterior electrónico' },
];
