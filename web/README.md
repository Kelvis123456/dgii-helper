# web

Interfaz en Angular 21 para `dgii-helper`: una pantalla para validar RNC, cédula y NCF, y otra para registrar rangos de NCF y entregar sus números.

Elegí Angular porque es el framework que más aparece en las ofertas empresariales de por aquí y quería tener un proyecto real con él, usando señales y componentes standalone en vez de módulos.

## Correrlo

```bash
docker compose up -d sql
dotnet run --project src/DgiiHelper.Api   # en la raíz, con ASPNETCORE_URLS=http://localhost:5080
cd web && npm install && npm start
```

`proxy.conf.json` manda `/api` a `http://localhost:5080`, así que el navegador no necesita CORS. Necesitas Node 20.19 o más nuevo.

```bash
npm test      # Vitest, 47 tests
npm run build
```

## Lo que hay que saber

- La clave de API se guarda en `sessionStorage`: sobrevive a recargar y desaparece al cerrar la pestaña. No la dejé en `localStorage` porque no quiero que quede en disco.
- Los mensajes de error están en español y salen de los códigos que devuelve la API, no del texto del servidor.
- No está desplegada ni tiene login: la clave de API es todo el control de acceso, igual que la API.
- Los tests cubren los componentes con `HttpTestingController`. Además lo recorrí en un navegador contra la API y SQL Server reales: validar un RNC bueno y uno malo, registrar un rango de 3 números, entregar los 3 y ver el botón pasar a «Rango agotado», y una clave incorrecta mostrando el aviso de 401.
