import { provideHttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { App } from './app';
import { routes } from './app.routes';

describe('App', () => {
  beforeEach(() =>
    TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter(routes), provideHttpClient()],
    }),
  );

  it('muestra la navegación con las dos pantallas', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    const links = [...(fixture.nativeElement as HTMLElement).querySelectorAll('nav a')].map((a) =>
      a.textContent?.trim(),
    );
    expect(links).toEqual(['Validar', 'Rangos de NCF']);
  });

  it('las rutas desconocidas vuelven a validar', () => {
    const fallback = routes.find((r) => r.path === '**');
    expect(fallback?.redirectTo).toBe('validar');
  });
});
