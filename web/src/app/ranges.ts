import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Observable } from 'rxjs';
import { Api, ApiKey, NewRange, Range } from './api';
import { describeError, ELECTRONIC_TYPES, TRADITIONAL_TYPES } from './messages';

type Kind = NewRange['kind'];

@Component({
  selector: 'app-ranges',
  imports: [FormsModule],
  templateUrl: './ranges.html',
})
export class Ranges {
  private readonly api = inject(Api);
  protected readonly apiKey = inject(ApiKey);

  protected readonly kind = signal<Kind>('traditional');
  protected readonly type = signal('01');
  protected readonly first = signal<number | null>(1);
  protected readonly last = signal<number | null>(500);
  protected readonly expiresOn = signal('');
  protected readonly lookupId = signal<number | null>(null);

  protected readonly range = signal<Range | null>(null);
  protected readonly issued = signal<string[]>([]);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly typeOptions = computed(() =>
    this.kind() === 'traditional' ? TRADITIONAL_TYPES : ELECTRONIC_TYPES,
  );
  protected readonly hasKey = computed(() => this.apiKey.value().trim().length > 0);
  protected readonly used = computed(() => {
    const r = this.range();
    return r ? r.last - r.first + 1 - r.remaining : 0;
  });
  protected readonly total = computed(() => {
    const r = this.range();
    return r ? r.last - r.first + 1 : 0;
  });

  protected changeKind(kind: Kind): void {
    this.kind.set(kind);
    this.type.set(this.typeOptions()[0].code);
  }

  protected create(): void {
    const first = this.first();
    const last = this.last();
    if (first === null || last === null || !this.expiresOn()) return;
    this.run(
      this.api.createRange({
        kind: this.kind(),
        type: this.type(),
        first,
        last,
        expiresOn: this.expiresOn(),
      }),
      (r) => {
        this.range.set(r);
        this.issued.set([]);
        this.lookupId.set(r.id);
      },
    );
  }

  protected lookup(): void {
    const id = this.lookupId();
    if (id === null) return;
    this.run(this.api.getRange(id), (r) => {
      this.range.set(r);
      this.issued.set([]);
    });
  }

  protected issueNext(): void {
    const r = this.range();
    if (!r) return;
    this.run(this.api.issue(r.id), (issued) => {
      this.issued.update((list) => [issued.ncf, ...list]);
      this.refresh(r.id);
    });
  }

  private refresh(id: number): void {
    this.api.getRange(id).subscribe({
      next: (r) => this.range.set(r),
      error: (e) => this.error.set(describeError(e)),
    });
  }

  private run<T>(call: Observable<T>, onOk: (value: T) => void): void {
    if (this.busy()) return;
    this.busy.set(true);
    this.error.set(null);
    call.subscribe({
      next: (value) => {
        onOk(value);
        this.busy.set(false);
      },
      error: (e) => {
        this.error.set(describeError(e));
        this.busy.set(false);
      },
    });
  }
}
