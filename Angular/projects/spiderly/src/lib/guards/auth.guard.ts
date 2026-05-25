import { Injectable } from '@angular/core';
import { CanActivate, Router } from '@angular/router';
import { Observable, filter, map, take } from 'rxjs';
import { AuthServiceBase } from '../services/auth.service.base';
import { ConfigServiceBase } from '../services/config.service.base';

@Injectable({
  providedIn: 'root',
})
export class AuthGuard implements CanActivate {
  constructor(
    private authService: AuthServiceBase,
    private router: Router,
    private config: ConfigServiceBase,
  ) {}

  canActivate(): Observable<boolean> {
    return this.checkAuth();
  }

  private checkAuth(): Observable<boolean> {
    return this.authService.user$.pipe(
      filter((user) => user !== undefined), // wait until the session is resolved (undefined = still loading)
      take(1),
      map((user) => {
        if (user) {
          return true;
        }
        this.router.navigate([this.config.loginSlug]);
        return false;
      }),
    );
  }
}
