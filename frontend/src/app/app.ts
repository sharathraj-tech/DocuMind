import { Component, signal } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { ConfirmDialog } from 'primeng/confirmdialog';
import { Toast } from 'primeng/toast';

@Component({
  imports: [RouterOutlet, Toast, ConfirmDialog],
  selector: 'app-root',
  template: `
    <router-outlet />
    <p-toast />
    <p-confirmdialog />
  `,
})
export class App {
  protected readonly title = signal('documind-web');
}
