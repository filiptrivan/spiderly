import { Injectable } from '@angular/core';
import { CanActivate } from '@angular/router';
import { Observable, map } from 'rxjs';
import { AuthServiceBase } from '../services/auth.service.base';

@Injectable({
  providedIn: 'root',
})
export class NotAuthGuard implements CanActivate {
  constructor(
    private authService: AuthServiceBase, 
  ) {

  }

  canActivate(): Observable<boolean> {
    return this.checkAuth();
  }

  private checkAuth(): Observable<boolean> {
    return this.authService.user$.pipe(
      map((user) => {
        if (user) {
          this.authService.navigateToDashboard();
          return false;
        } 
        else {
          return true;
        }
      })
    );
  }
}
