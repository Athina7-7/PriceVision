import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { catchError, throwError } from 'rxjs';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const token = typeof window !== 'undefined' ? localStorage.getItem('jwt_token') : null;
  const cloned = token ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req;

  return next(cloned).pipe(
    catchError((err: HttpErrorResponse) => {
      if (err.status === 401 || err.status === 403) {
        localStorage.removeItem('jwt_token');
        localStorage.removeItem('user_role');
        if (typeof window !== 'undefined' && err.status === 401) window.location.reload();
      }
      return throwError(() => err);
    })
  );
};