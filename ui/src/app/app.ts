import { Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { SelectSearchService } from './core/services/select-search.service';
import { ThemeService } from './core/services/theme.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet],
  template: '<router-outlet />',
})
export class App {
  // Instantiated eagerly so the color scheme is applied on startup.
  protected readonly theme = inject(ThemeService);
  // Instantiated eagerly so long dropdowns get a search box.
  protected readonly selectSearch = inject(SelectSearchService);
}
