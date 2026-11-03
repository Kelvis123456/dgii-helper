import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Api, IdKind, IdResult } from './api';
import { describeError, describeProblem } from './messages';

interface KindOption {
  value: IdKind;
  label: string;
  placeholder: string;
}

@Component({
  selector: 'app-validate',
  imports: [FormsModule],
  templateUrl: './validate.html',
})
export class Validate {
  private readonly api = inject(Api);

  protected readonly kinds: KindOption[] = [
    { value: 'rnc', label: 'RNC', placeholder: '1-01-01010-1' },
    { value: 'cedula', label: 'Cédula', placeholder: '001-1234567-3' },
    { value: 'ncf', label: 'NCF o e-CF', placeholder: 'B0100000001' },
  ];

  protected readonly kind = signal<IdKind>('rnc');
  protected readonly value = signal('');
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  /** Lo último que se consultó, con el tipo que tenía en ese momento (cambiar el selector no reescribe el resultado). */
  protected readonly checked = signal<{ kind: IdKind; result: IdResult } | null>(null);

  protected readonly placeholder = computed(
    () => this.kinds.find((k) => k.value === this.kind())!.placeholder,
  );
  protected readonly problem = computed(() => {
    const c = this.checked();
    return c && !c.result.valid ? describeProblem(c.kind, c.result.problem) : null;
  });

  protected submit(): void {
    const value = this.value().trim();
    if (!value || this.busy()) return;
    const kind = this.kind();

    // Un valor como ".." no se codifica en la URL y el navegador lo normaliza a /api/, que da un 404 engañoso.
    if (!/^[0-9A-Za-z\- ]+$/.test(value)) {
      this.checked.set(null);
      this.error.set('Usa solo letras, números y guiones.');
      return;
    }

    this.busy.set(true);
    this.error.set(null);
    this.api.validate(kind, value).subscribe({
      next: (result) => {
        this.checked.set({ kind, result });
        this.busy.set(false);
      },
      error: (e) => {
        this.checked.set(null);
        this.error.set(describeError(e));
        this.busy.set(false);
      },
    });
  }
}
