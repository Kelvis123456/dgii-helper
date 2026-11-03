import { HttpErrorResponse } from '@angular/common/http';
import { describeError, describeProblem, ELECTRONIC_TYPES, TRADITIONAL_TYPES } from './messages';

const GENERIC = 'No es válido.';

describe('describeProblem', () => {
  it.each([
    ['rnc', ['length', 'notdigits', 'checkdigit', 'zeros']],
    ['cedula', ['length', 'notdigits', 'checkdigit', 'zeros']],
    ['ncf', ['length', 'series', 'not_digits', 'type', 'sequence']],
  ] as const)('%s: cada motivo que devuelve la API tiene su propia frase', (kind, problems) => {
    const messages = problems.map((p) => describeProblem(kind, p));
    expect(messages.every((m) => m !== GENERIC)).toBe(true);
    expect(new Set(messages).size).toBe(problems.length);
  });

  it('un motivo desconocido o ausente cae en la frase genérica', () => {
    expect(describeProblem('rnc', 'inventado')).toBe(GENERIC);
    expect(describeProblem('rnc', null)).toBe(GENERIC);
    expect(describeProblem('ncf', undefined)).toBe(GENERIC);
  });

  it('el mismo motivo se explica distinto según el tipo', () => {
    expect(describeProblem('rnc', 'length')).toContain('9');
    expect(describeProblem('cedula', 'length')).toContain('11');
  });
});

const failure = (status: number, code?: string) =>
  new HttpErrorResponse({ status, error: code ? { error: code } : null });

describe('describeError', () => {
  it.each([
    [0, undefined, 'conectar'],
    [401, undefined, 'clave'],
    [404, undefined, 'no existe'],
    [409, 'exhausted', 'agotó'],
    [409, 'overlap', 'solapa'],
    [410, undefined, 'venció'],
    [429, undefined, 'Demasiadas'],
    [400, 'bounds', 'rango'],
    [400, 'expired', 'vencimiento'],
    [400, 'type', 'tipo'],
    [500, undefined, '500'],
  ])('estado %i (%s) menciona "%s"', (status, code, fragment) => {
    expect(describeError(failure(status, code))).toContain(fragment);
  });

  it('un 400 con un código que no conoce da una frase general en vez de callar', () => {
    expect(describeError(failure(400, 'algo-nuevo'))).toBe('La API rechazó los datos.');
  });

  it('algo que no es una respuesta HTTP también se explica', () => {
    expect(describeError(new Error('boom'))).toBe('Algo falló. Intenta de nuevo.');
  });
});

describe('tipos de comprobante', () => {
  it('coinciden con los que acepta la API', () => {
    expect(TRADITIONAL_TYPES.map((t) => t.code)).toEqual([
      '01',
      '02',
      '03',
      '04',
      '11',
      '12',
      '13',
      '14',
      '15',
      '16',
      '17',
    ]);
    expect(ELECTRONIC_TYPES.map((t) => t.code)).toEqual([
      '31',
      '32',
      '33',
      '34',
      '41',
      '43',
      '44',
      '45',
      '46',
      '47',
    ]);
  });
});
