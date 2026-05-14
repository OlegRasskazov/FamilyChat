import { HttpClient } from '@angular/common/http';
import { Component, OnInit, signal } from '@angular/core';

interface WeatherForecast {
  date: string;
  temperatureC: number;
  temperatureF: number;
  summary: string;
}

interface AuthUser {
  isAuthenticated: boolean;
  name?: string;
  email?: string;
  pictureUrl?: string;
}

@Component({
  selector: 'app-root',
  templateUrl: './app.html',
  standalone: false,
  styleUrl: './app.css'
})
export class App implements OnInit {
  public forecasts = signal<WeatherForecast[]>([]);
  public currentUser = signal<AuthUser | null>(null);
  public isLoginDialogOpen = signal(false);

  constructor(private http: HttpClient) {}

  ngOnInit() {
    this.loadCurrentUser();
    this.http.get<WeatherForecast[]>('/weatherforecast').subscribe({
      next: (result) => {
        this.forecasts.set(result);
      },
      error: (error) => {
        console.error(error);
      }
    });
  }

  public openLoginDialog(): void {
    this.isLoginDialogOpen.set(true);
  }

  public closeLoginDialog(): void {
    this.isLoginDialogOpen.set(false);
  }

  public loginWithGoogle(): void {
    const returnUrl = encodeURIComponent(window.location.pathname + window.location.search);
    window.location.href = `/auth/login/google?returnUrl=${returnUrl}`;
  }

  public logout(): void {
    const returnUrl = encodeURIComponent(window.location.pathname + window.location.search);
    window.location.href = `/auth/logout?returnUrl=${returnUrl}`;
  }

  private loadCurrentUser(): void {
    this.http.get<AuthUser>('/auth/me').subscribe({
      next: (user) => {
        this.currentUser.set(user.isAuthenticated ? user : null);
      },
      error: () => {
        this.currentUser.set(null);
      }
    });
  }

  protected readonly title = signal('familychat.client');
}
