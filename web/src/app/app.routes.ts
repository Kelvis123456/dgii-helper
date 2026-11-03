import { Routes } from '@angular/router';
import { Ranges } from './ranges';
import { Validate } from './validate';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'validar' },
  { path: 'validar', component: Validate, title: 'Validar | dgii-helper' },
  { path: 'rangos', component: Ranges, title: 'Rangos de NCF | dgii-helper' },
  { path: '**', redirectTo: 'validar' },
];
