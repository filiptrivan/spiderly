import { Injectable } from '@angular/core';
import { CanActivate } from '@angular/router';
import { Observable, filter, map, take } from 'rxjs';
import { AuthServiceBase } from '../services/auth.service.base';

@Injectable({
  providedIn: 'root',
})
export class NotAuthGuard implements CanActivate {
  constructor(private authService: AuthServiceBase) {}

  canActivate(): Observable<boolean> {
    return this.checkAuth();
  }

  private checkAuth(): Observable<boolean> {
    return this.authService.user$.pipe(
      filter((user) => user !== undefined), // wait until the session is resolved (undefined = still loading)
      take(1),
      map((user) => {
        if (user) {
          this.authService.navigateToDashboard();
          return false;
        }
        return true;
      }),
    );
  }
}
