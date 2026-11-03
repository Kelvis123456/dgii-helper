import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ApiKey, Range } from './api';
import { Ranges } from './ranges';

let fixture: ComponentFixture<Ranges>;
let http: HttpTestingController;

const el = () => fixture.nativeElement as HTMLElement;
const q = <T extends HTMLElement>(selector: string) => el().querySelector(selector) as T;
const buttons = () => [...el().querySelectorAll('button')];
const button = (label: string) =>
  buttons().find((b) => b.textContent?.includes(label)) as HTMLButtonElement;

async function fill(selector: string, value: string, event = 'input') {
  const field = q<HTMLInputElement | HTMLSelectElement>(selector);
  field.value = value;
  field.dispatchEvent(new Event(event));
  await fixture.whenStable();
}

async function submit(formIndex: number) {
  el().querySelectorAll('form')[formIndex].dispatchEvent(new Event('submit'));
  await fixture.whenStable();
}

const range = (over: Partial<Range> = {}): Range => ({
  id: 3,
  kind: 'traditional',
  type: '01',
  first: 1,
  last: 5,
  next: 1,
  remaining: 5,
  expiresOn: '2027-12-31',
  ...over,
});

async function registerRange() {
  await fill('#expires', '2027-12-31');
  await submit(0);
  http.expectOne('/api/ncf/ranges').flush(range(), { status: 201, statusText: 'Created' });
  await fixture.whenStable();
}

describe('Ranges', () => {
  beforeEach(async () => {
    sessionStorage.clear();
    TestBed.configureTestingModule({
      imports: [Ranges],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(Ranges);
    await fixture.whenStable();
  });

  afterEach(() => http.verify());

  it('sin clave no deja registrar ni consultar y avisa', async () => {
    await fill('#expires', '2027-12-31');
    expect(button('Registrar').disabled).toBe(true);
    expect(button('Consultar').disabled).toBe(true);
    expect(el().textContent).toContain('Escribe la clave de API');
  });

  it('con clave y fecha deja registrar', async () => {
    TestBed.inject(ApiKey).set('k');
    await fill('#expires', '2027-12-31');
    expect(button('Registrar').disabled).toBe(false);
  });

  it('cambiar de serie reinicia el tipo al primero de esa serie', async () => {
    expect(q<HTMLSelectElement>('#type').value).toContain('01');
    await fill('#kind', 'electronic', 'change');
    expect(q<HTMLSelectElement>('#type').options[0].value).toBe('31');
    await fill('#kind', 'traditional', 'change');
    expect(q<HTMLSelectElement>('#type').options[0].value).toBe('01');
  });

  it('registra el rango con lo que se escribió y muestra el panel', async () => {
    TestBed.inject(ApiKey).set('k');
    await fill('#kind', 'electronic', 'change');
    await fill('#first', '10');
    await fill('#last', '20');
    await fill('#expires', '2027-12-31');
    await submit(0);

    const req = http.expectOne('/api/ncf/ranges');
    expect(req.request.body).toEqual({
      kind: 'electronic',
      type: '31',
      first: 10,
      last: 20,
      expiresOn: '2027-12-31',
    });
    req.flush(
      range({
        id: 3,
        kind: 'electronic',
        type: '31',
        first: 10,
        last: 20,
        next: 10,
        remaining: 11,
      }),
      {
        status: 201,
        statusText: 'Created',
      },
    );
    await fixture.whenStable();

    expect(el().querySelector('#range-title')?.textContent).toBe('Rango 3');
    expect(el().querySelector('.panel')?.textContent).toContain('11 de 11');
  });

  it('entrega el siguiente número, lo anota y actualiza lo que queda', async () => {
    TestBed.inject(ApiKey).set('k');
    await registerRange();

    button('Entregar').click();
    await fixture.whenStable();
    http
      .expectOne('/api/ncf/ranges/3/issue')
      .flush({ ncf: 'B0100000001', sequence: 1 }, { status: 201, statusText: 'Created' });
    await fixture.whenStable();
    http.expectOne('/api/ncf/ranges/3').flush(range({ next: 2, remaining: 4 }));
    await fixture.whenStable();

    expect(el().querySelector('.issued')?.textContent).toContain('B0100000001');
    expect(el().querySelector('.panel')?.textContent).toContain('4 de 5');
    expect(q<HTMLProgressElement>('progress').value).toBe(1);
  });

  it('los números entregados se listan del más nuevo al más viejo', async () => {
    TestBed.inject(ApiKey).set('k');
    await registerRange();

    for (const [seq, remaining] of [
      [1, 4],
      [2, 3],
    ] as const) {
      button('Entregar').click();
      await fixture.whenStable();
      http
        .expectOne('/api/ncf/ranges/3/issue')
        .flush({ ncf: `B010000000${seq}`, sequence: seq }, { status: 201, statusText: 'Created' });
      await fixture.whenStable();
      http.expectOne('/api/ncf/ranges/3').flush(range({ next: seq + 1, remaining }));
      await fixture.whenStable();
    }

    const items = [...el().querySelectorAll('.issued li')].map((li) => li.textContent);
    expect(items).toEqual(['B0100000002', 'B0100000001']);
  });

  it('un rango sin números queda marcado como agotado y no deja entregar', async () => {
    TestBed.inject(ApiKey).set('k');
    await fill('#lookup', '3');
    await submit(1);
    http.expectOne('/api/ncf/ranges/3').flush(range({ next: 6, remaining: 0 }));
    await fixture.whenStable();

    const exhausted = button('Rango agotado');
    expect(exhausted.disabled).toBe(true);
  });

  it('si la API dice que se agotó, lo explica', async () => {
    TestBed.inject(ApiKey).set('k');
    await registerRange();

    button('Entregar').click();
    await fixture.whenStable();
    http
      .expectOne('/api/ncf/ranges/3/issue')
      .flush({ error: 'exhausted' }, { status: 409, statusText: 'Conflict' });
    await fixture.whenStable();

    expect(el().querySelector('[role="alert"]')?.textContent).toContain('agotó');
  });

  it('con una clave equivocada lo dice', async () => {
    TestBed.inject(ApiKey).set('mala');
    await fill('#expires', '2027-12-31');
    await submit(0);
    http.expectOne('/api/ncf/ranges').flush(null, { status: 401, statusText: 'Unauthorized' });
    await fixture.whenStable();

    expect(el().querySelector('[role="alert"]')?.textContent).toContain('clave');
    expect(el().querySelector('.panel')).toBeNull();
  });

  it('un rango solapado se explica y no cambia lo que había en pantalla', async () => {
    TestBed.inject(ApiKey).set('k');
    await registerRange();

    await submit(0);
    http
      .expectOne('/api/ncf/ranges')
      .flush({ error: 'overlap' }, { status: 409, statusText: 'Conflict' });
    await fixture.whenStable();

    expect(el().querySelector('[role="alert"]')?.textContent).toContain('solapa');
    expect(el().querySelector('#range-title')?.textContent).toBe('Rango 3');
  });

  it('un segundo clic mientras espera no entrega dos números', async () => {
    TestBed.inject(ApiKey).set('k');
    await registerRange();

    button('Entregar').click();
    button('Entregar').click();
    await fixture.whenStable();
    http.expectOne('/api/ncf/ranges/3/issue');
  });
});
