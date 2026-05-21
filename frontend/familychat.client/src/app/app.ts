import { HttpClient } from '@angular/common/http';
import { Component, OnInit, signal } from '@angular/core';
import { ACCESS_TOKEN_STORAGE_KEY, REFRESH_TOKEN_STORAGE_KEY } from './auth-token.interceptor';

interface AuthUser {
  isAuthenticated: boolean;
  id?: string;
  name?: string;
  email?: string;
  pictureUrl?: string;
}

interface AuthTokenResponse {
  accessToken: string;
  expiresIn: number;
  refreshToken?: string;
  tokenType: string;
}

@Component({
  selector: 'app-root',
  templateUrl: './app.html',
  standalone: false,
  styleUrl: './app.css'
})
export class App implements OnInit {
  public currentUser = signal<AuthUser | null>(null);
  public isLoginDialogOpen = signal(false);

  constructor(private http: HttpClient) {}

  ngOnInit() {
    this.exchangeGoogleLoginCode();
    this.loadCurrentUser();
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
    localStorage.removeItem(ACCESS_TOKEN_STORAGE_KEY);
    localStorage.removeItem(REFRESH_TOKEN_STORAGE_KEY);
    this.currentUser.set(null);
    this.http.post('/auth/logout', {}).subscribe();
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

  private exchangeGoogleLoginCode(): void {
    const url = new URL(window.location.href);
    const code = url.searchParams.get('googleLoginCode');
    const error = url.searchParams.get('googleLoginError');

    if (error) {
      console.error(error);
      this.removeGoogleLoginQueryParams(url);
      return;
    }

    if (!code) {
      return;
    }

    this.removeGoogleLoginQueryParams(url);
    this.http.post<AuthTokenResponse>('/auth/login/google/token', { code }).subscribe({
      next: (tokenResponse) => {
        localStorage.setItem(ACCESS_TOKEN_STORAGE_KEY, tokenResponse.accessToken);

        if (tokenResponse.refreshToken) {
          localStorage.setItem(REFRESH_TOKEN_STORAGE_KEY, tokenResponse.refreshToken);
        }

        this.loadCurrentUser();
      },
      error: (exchangeError) => {
        console.error(exchangeError);
      }
    });
  }

  private removeGoogleLoginQueryParams(url: URL): void {
    url.searchParams.delete('googleLoginCode');
    url.searchParams.delete('googleLoginError');
    window.history.replaceState({}, document.title, url.pathname + url.search + url.hash);
  }

  protected readonly title = signal('familychat.client');
}
