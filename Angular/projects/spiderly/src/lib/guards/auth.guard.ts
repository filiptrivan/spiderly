import { Injectable } from '@angular/core';
import { CanActivate, Router } from '@angular/router';
import { Observable, map } from 'rxjs';
import { AuthServiceBase } from '../services/auth.service.base';
import { ConfigServiceBase } from '../services/config.service.base';

@Injectable({
  providedIn: 'root',
})
export class AuthGuard implements CanActivate {
  constructor(
    private authService: AuthServiceBase, 
    private router: Router,
    private config: ConfigServiceBase
  ) {

  }

  canActivate(): Observable<boolean> {
    return this.checkAuth();
  }

  private checkAuth(): Observable<boolean> {
    return this.authService.user$.pipe(
      map((user) => {
        if (user) {
          return true;
        } else {
          // const returnUrl = this.router.getCurrentNavigation()?.extractedUrl.toString() || '/';
          // this.router.navigate(['login'], {
          //    queryParams: { returnUrl },
          // });
          this.router.navigate([this.config.loginSlug]);
          return false;
        }
      })
    );
  }
}
