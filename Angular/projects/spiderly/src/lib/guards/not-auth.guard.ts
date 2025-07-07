import { Injectable } from '@angular/core';
import { CanActivate } from '@angular/router';
import { Observable, map } from 'rxjs';
import { AuthBaseService } from '../services/auth-base.service';

@Injectable({
  providedIn: 'root',
})
export class NotAuthGuard implements CanActivate {
  constructor(
    private authService: AuthBaseService, 
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
