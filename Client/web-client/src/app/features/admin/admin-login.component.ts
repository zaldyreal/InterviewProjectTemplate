import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';

import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-admin-login',
  standalone: true,
  imports: [ReactiveFormsModule],
  templateUrl: './admin-login.component.html'
})
export class AdminLoginComponent {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected readonly isSubmitting = signal(false);
  protected readonly errorMessage = signal<string | null>(null);

  /** Set when the guard or interceptor redirected here because a session ended. */
  protected readonly sessionExpired = signal(
    this.route.snapshot.queryParamMap.get('expired') === 'true'
  );

  protected readonly form = inject(FormBuilder).nonNullable.group({
    username: ['', Validators.required],
    password: ['', Validators.required]
  });

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.isSubmitting.set(true);
    this.errorMessage.set(null);
    this.sessionExpired.set(false);

    this.authService.login(this.form.getRawValue()).subscribe({
      next: () => {
        // Returns the admin to whatever they originally asked for, defaulting to the report.
        const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl') ?? '/admin';

        void this.router.navigateByUrl(returnUrl);
      },
      error: (error: HttpErrorResponse) => {
        this.isSubmitting.set(false);

        this.errorMessage.set(
          error.status === 0
            ? 'Could not reach the server. Please check your connection and try again.'
            : error.error?.detail ?? 'The username or password is incorrect.'
        );
      }
    });
  }
}
