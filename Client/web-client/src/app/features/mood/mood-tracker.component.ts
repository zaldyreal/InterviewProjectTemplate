import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';

import { MoodService } from '../../core/services/mood.service';
import { MoodEntry, MoodOption, MoodRating } from '../../core/models/mood.models';

/** The comment column is bounded at 1000 characters server-side; the form matches it. */
const COMMENT_MAX_LENGTH = 1000;

@Component({
  selector: 'app-mood-tracker',
  standalone: true,
  imports: [ReactiveFormsModule],
  templateUrl: './mood-tracker.component.html',
  styleUrl: './mood-tracker.component.scss'
})
export class MoodTrackerComponent implements OnInit {
  private readonly moodService = inject(MoodService);
  private readonly formBuilder = inject(FormBuilder);

  protected readonly commentMaxLength = COMMENT_MAX_LENGTH;

  protected readonly options = signal<MoodOption[]>([]);
  protected readonly isLoading = signal(true);
  protected readonly isSubmitting = signal(false);

  /** Set when the user has recorded a mood today, whether just now or on an earlier visit. */
  protected readonly submittedEntry = signal<MoodEntry | null>(null);

  /**
   * True when the API rejected the submission because a mood already exists for today. Held
   * separately from `errorMessage` so the template can present it as an expected outcome rather
   * than a failure.
   */
  protected readonly alreadySubmitted = signal(false);

  protected readonly errorMessage = signal<string | null>(null);

  protected readonly form = this.formBuilder.nonNullable.group({
    rating: this.formBuilder.control<MoodRating | null>(null, Validators.required),
    comment: ['', Validators.maxLength(COMMENT_MAX_LENGTH)]
  });

  ngOnInit(): void {
    this.loadInitialState();
  }

  protected selectRating(rating: MoodRating): void {
    this.form.controls.rating.setValue(rating);
    this.form.controls.rating.markAsTouched();
  }

  protected submit(): void {
    if (this.form.invalid) {
      // Marks the group touched so the "please choose a mood" hint appears when the user submits
      // without picking one, rather than the click appearing to do nothing.
      this.form.markAllAsTouched();
      return;
    }

    const { rating, comment } = this.form.getRawValue();

    this.isSubmitting.set(true);
    this.errorMessage.set(null);

    this.moodService
      .submit({ rating: rating as MoodRating, comment: comment.trim() || null })
      .subscribe({
        next: entry => {
          this.submittedEntry.set(entry);
          this.isSubmitting.set(false);
        },
        error: (error: HttpErrorResponse) => {
          this.isSubmitting.set(false);
          this.handleSubmitError(error);
        }
      });
  }

  private handleSubmitError(error: HttpErrorResponse): void {
    if (error.status === 409) {
      // The expected duplicate case. The message comes from the API so the wording lives in one
      // place, with a fallback in case the response body is not the expected shape.
      this.alreadySubmitted.set(true);
      this.errorMessage.set(
        error.error?.detail ?? 'You have already recorded your mood today.'
      );

      // Re-reads today's entry so the page can show what was actually recorded rather than just
      // an error. Common when the form was left open across midnight or in a second tab.
      this.moodService.getTodayStatus().subscribe({
        next: status => this.submittedEntry.set(status.entry),
        error: () => undefined
      });

      return;
    }

    if (error.status === 0) {
      this.errorMessage.set(
        'Could not reach the server. Please check your connection and try again.'
      );
      return;
    }

    this.errorMessage.set(
      error.error?.detail ?? 'Something went wrong saving your mood. Please try again.'
    );
  }

  private loadInitialState(): void {
    this.moodService.getOptions().subscribe({
      next: options => this.options.set(options),
      error: () =>
        this.errorMessage.set('Could not load the mood options. Please refresh the page.')
    });

    // Asking the API up front means a returning user sees their recorded mood immediately instead
    // of being shown a form that is guaranteed to be rejected.
    this.moodService.getTodayStatus().subscribe({
      next: status => {
        if (status.hasSubmittedToday) {
          this.submittedEntry.set(status.entry);
          this.alreadySubmitted.set(true);
        }

        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false)
    });
  }
}
