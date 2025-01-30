import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { VideoDashboardComponent } from './components/video-dashboard/video-dashboard.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, VideoDashboardComponent],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss'
})
export class AppComponent {
  title = 'axis-poc-ui';
}
