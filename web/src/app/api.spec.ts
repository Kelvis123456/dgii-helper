import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Api, ApiKey } from './api';

function setup() {
  TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
  return {
    api: TestBed.inject(Api),
    key: TestBed.inject(ApiKey),
    http: TestBed.inject(HttpTestingController),
  };
}

describe('Api', () => {
  beforeEach(() => sessionStorage.clear());

  it('valida sin mandar la clave y recorta los espacios', () => {
    const { api, http } = setup();
    api.validate('rnc', '  1-01-01010-1 ').subscribe();

    const req = http.expectOne('/api/rnc/1-01-01010-1');
    expect(req.request.method).toBe('GET');
    expect(req.request.headers.has('X-Api-Key')).toBe(false);
  });

  it('codifica el valor para que una barra no cambie la ruta', () => {
    const { api, http } = setup();
    api.validate('ncf', 'B01/..').subscribe();
    http.expectOne('/api/ncf/B01%2F..');
  });

  it('manda la clave al registrar, consultar y entregar', () => {
    const { api, key, http } = setup();
    key.set('mi-clave');

    api
      .createRange({ kind: 'traditional', type: '01', first: 1, last: 5, expiresOn: '2027-12-31' })
      .subscribe();
    const create = http.expectOne('/api/ncf/ranges');
    expect(create.request.method).toBe('POST');
    expect(create.request.headers.get('X-Api-Key')).toBe('mi-clave');
    expect(create.request.body).toEqual({
      kind: 'traditional',
      type: '01',
      first: 1,
      last: 5,
      expiresOn: '2027-12-31',
    });

    api.getRange(7).subscribe();
    expect(http.expectOne('/api/ncf/ranges/7').request.headers.get('X-Api-Key')).toBe('mi-clave');

    api.issue(7).subscribe();
    const issue = http.expectOne('/api/ncf/ranges/7/issue');
    expect(issue.request.method).toBe('POST');
    expect(issue.request.headers.get('X-Api-Key')).toBe('mi-clave');
  });
});

describe('ApiKey', () => {
  beforeEach(() => sessionStorage.clear());

  it('arranca vacía si no hay nada guardado', () => {
    TestBed.configureTestingModule({});
    expect(TestBed.inject(ApiKey).value()).toBe('');
  });

  it('se guarda en sessionStorage y se lee al volver a crearla', () => {
    TestBed.configureTestingModule({});
    TestBed.inject(ApiKey).set('abc');
    expect(sessionStorage.getItem('dgii-helper-api-key')).toBe('abc');

    TestBed.resetTestingModule();
    expect(TestBed.inject(ApiKey).value()).toBe('abc');
  });

  it('borrarla la saca del almacenamiento', () => {
    TestBed.configureTestingModule({});
    const key = TestBed.inject(ApiKey);
    key.set('abc');
    key.set('');
    expect(sessionStorage.getItem('dgii-helper-api-key')).toBeNull();
    expect(key.value()).toBe('');
  });

  it('si el almacenamiento falla, la clave queda en memoria', () => {
    TestBed.configureTestingModule({});
    const spy = vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('bloqueado');
    });
    const key = TestBed.inject(ApiKey);
    expect(() => key.set('abc')).not.toThrow();
    expect(key.value()).toBe('abc');
    spy.mockRestore();
  });
});
