import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Validate } from './validate';

let fixture: ComponentFixture<Validate>;
let http: HttpTestingController;

const el = () => fixture.nativeElement as HTMLElement;
const q = <T extends HTMLElement>(selector: string) => el().querySelector(selector) as T;

async function type(value: string) {
  const input = q<HTMLInputElement>('#value');
  input.value = value;
  input.dispatchEvent(new Event('input'));
  await fixture.whenStable();
}

async function choose(kind: string) {
  const select = q<HTMLSelectElement>('#kind');
  select.value = kind;
  select.dispatchEvent(new Event('change'));
  await fixture.whenStable();
}

async function submit() {
  q<HTMLFormElement>('form').dispatchEvent(new Event('submit'));
  await fixture.whenStable();
}

describe('Validate', () => {
  beforeEach(async () => {
    TestBed.configureTestingModule({
      imports: [Validate],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(Validate);
    await fixture.whenStable();
  });

  afterEach(() => http.verify());

  it('no deja enviar mientras el campo está vacío', async () => {
    expect(q<HTMLButtonElement>('button').disabled).toBe(true);
    await type('   ');
    expect(q<HTMLButtonElement>('button').disabled).toBe(true);
    await type('101010101');
    expect(q<HTMLButtonElement>('button').disabled).toBe(false);
  });

  it('muestra un RNC válido con sus guiones', async () => {
    await type('101010101');
    await submit();
    http
      .expectOne('/api/rnc/101010101')
      .flush({ valid: true, formatted: '1-01-01010-1', problem: null });
    await fixture.whenStable();

    expect(el().querySelector('.result.ok')?.textContent).toContain('Válido');
    expect(el().querySelector('.formatted')?.textContent).toBe('1-01-01010-1');
  });

  it('explica por qué un identificador no es válido', async () => {
    await type('101010100');
    await submit();
    http
      .expectOne('/api/rnc/101010100')
      .flush({ valid: false, formatted: null, problem: 'checkdigit' });
    await fixture.whenStable();

    const bad = el().querySelector('.result.bad');
    expect(bad?.textContent).toContain('No es válido');
    expect(bad?.textContent).toContain('dígito verificador');
  });

  it('usa la ruta del tipo elegido', async () => {
    await choose('cedula');
    await type('001-1234567-3');
    await submit();
    http
      .expectOne('/api/cedula/001-1234567-3')
      .flush({ valid: true, formatted: '001-1234567-3', problem: null });
  });

  it('para un NCF muestra la serie, el tipo y la secuencia', async () => {
    await choose('ncf');
    await type('E310000000150');
    await submit();
    http.expectOne('/api/ncf/E310000000150').flush({
      valid: true,
      kind: 'electronic',
      type: '31',
      typeName: 'Factura de crédito fiscal electrónica',
      sequence: 150,
    });
    await fixture.whenStable();

    const text = el().querySelector('.result.ok')?.textContent ?? '';
    expect(text).toContain('Electrónica (e-CF)');
    expect(text).toContain('31, Factura de crédito fiscal electrónica');
    expect(text).toContain('150');
  });

  it('cambiar el tipo después de validar no reescribe el resultado anterior', async () => {
    await type('1234');
    await submit();
    http.expectOne('/api/rnc/1234').flush({ valid: false, formatted: null, problem: 'length' });
    await fixture.whenStable();
    expect(el().querySelector('.result.bad')?.textContent).toContain('9 dígitos');

    await choose('cedula');
    expect(el().querySelector('.result.bad')?.textContent).toContain('9 dígitos');
  });

  it('si la API no responde lo dice y borra el resultado anterior', async () => {
    await type('101010101');
    await submit();
    http
      .expectOne('/api/rnc/101010101')
      .flush({ valid: true, formatted: '1-01-01010-1', problem: null });
    await fixture.whenStable();

    await submit();
    http.expectOne('/api/rnc/101010101').error(new ProgressEvent('error'));
    await fixture.whenStable();

    expect(el().querySelector('[role="alert"]')?.textContent).toContain('No se pudo conectar');
    expect(el().querySelector('.result.ok')).toBeNull();
  });

  it('un segundo envío mientras espera no manda otra petición', async () => {
    await type('101010101');
    await submit();
    await submit();
    http.expectOne('/api/rnc/101010101');
  });
});
