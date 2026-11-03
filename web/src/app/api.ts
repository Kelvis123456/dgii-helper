import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Observable } from 'rxjs';

export type IdKind = 'rnc' | 'cedula' | 'ncf';

export interface IdResult {
  valid: boolean;
  formatted?: string | null;
  problem?: string | null;
  kind?: string;
  type?: string;
  typeName?: string;
  sequence?: number;
}

export interface Range {
  id: number;
  kind: 'traditional' | 'electronic';
  type: string;
  first: number;
  last: number;
  next: number;
  remaining: number;
  expiresOn: string;
}

export interface NewRange {
  kind: 'traditional' | 'electronic';
  type: string;
  first: number;
  last: number;
  expiresOn: string;
}

export interface Issued {
  ncf: string;
  sequence: number;
}

const KEY_STORAGE = 'dgii-helper-api-key';

/** La clave vive en sessionStorage: sobrevive a recargar la pestaña y desaparece al cerrarla. */
@Injectable({ providedIn: 'root' })
export class ApiKey {
  readonly value = signal(read());

  set(key: string): void {
    this.value.set(key);
    try {
      if (key) sessionStorage.setItem(KEY_STORAGE, key);
      else sessionStorage.removeItem(KEY_STORAGE);
    } catch {
      // Sin almacenamiento (ventana privada, bloqueado): la clave queda solo en memoria.
    }
  }
}

function read(): string {
  try {
    return sessionStorage.getItem(KEY_STORAGE) ?? '';
  } catch {
    return '';
  }
}

@Injectable({ providedIn: 'root' })
export class Api {
  private readonly http = inject(HttpClient);
  private readonly apiKey = inject(ApiKey);

  validate(kind: IdKind, value: string): Observable<IdResult> {
    return this.http.get<IdResult>(`/api/${kind}/${encodeURIComponent(value.trim())}`);
  }

  createRange(range: NewRange): Observable<Range> {
    return this.http.post<Range>('/api/ncf/ranges', range, this.auth());
  }

  getRange(id: number): Observable<Range> {
    return this.http.get<Range>(`/api/ncf/ranges/${id}`, this.auth());
  }

  issue(id: number): Observable<Issued> {
    return this.http.post<Issued>(`/api/ncf/ranges/${id}/issue`, null, this.auth());
  }

  private auth() {
    return { headers: { 'X-Api-Key': this.apiKey.value() } };
  }
}
