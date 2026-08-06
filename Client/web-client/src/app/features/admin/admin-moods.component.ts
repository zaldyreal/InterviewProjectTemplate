import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, computed, inject, signal } from '@angular/core';

import { MoodService } from '../../core/services/mood.service';
import { AdminMoodEntry, MoodRating } from '../../core/models/mood.models';

const PAGE_SIZE = 25;

@Component({
  selector: 'app-admin-moods',
  standalone: true,
  imports: [DatePipe],
  templateUrl: './admin-moods.component.html',
  styleUrl: './admin-moods.component.scss'
})
export class AdminMoodsComponent implements OnInit {
  private readonly moodService = inject(MoodService);

  protected readonly entries = signal<AdminMoodEntry[]>([]);
  protected readonly totalCount = signal(0);
  protected readonly page = signal(1);
  protected readonly totalPages = signal(0);
  protected readonly isLoading = signal(true);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly hasPreviousPage = computed(() => this.page() > 1);
  protected readonly hasNextPage = computed(() => this.page() < this.totalPages());

  /**
   * A simple count per mood across the current page. Deliberately labelled as covering the loaded
   * page only — presenting it as an all-time summary would be misleading once the data is paged.
   */
  protected readonly summary = computed(() => {
    const counts = new Map<string, number>();

    for (const entry of this.entries()) {
      counts.set(entry.ratingLabel, (counts.get(entry.ratingLabel) ?? 0) + 1);
    }

    return [...counts.entries()].map(([label, count]) => ({ label, count }));
  });

  ngOnInit(): void {
    this.load(1);
  }

  protected goToPage(page: number): void {
    if (page < 1 || (this.totalPages() > 0 && page > this.totalPages())) {
      return;
    }

    this.load(page);
  }

  /** Maps a rating onto a CSS modifier so the table is scannable at a glance. */
  protected ratingClass(rating: MoodRating): string {
    switch (rating) {
      case MoodRating.NotGoodAtAll:
        return 'pill--bad';
      case MoodRating.ABitMeh:
        return 'pill--meh';
      case MoodRating.PrettyGood:
        return 'pill--good';
      case MoodRating.FeelingGreat:
        return 'pill--great';
      default:
        return '';
    }
  }

  private load(page: number): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    this.moodService.getAllForAdmin(page, PAGE_SIZE).subscribe({
      next: response => {
        this.entries.set(response.items);
        this.totalCount.set(response.totalCount);
        this.totalPages.set(response.totalPages);
        this.page.set(response.page);
        this.isLoading.set(false);
      },
      error: (error: HttpErrorResponse) => {
        this.isLoading.set(false);

        // 401/403 is handled by the interceptor, which signs the admin out and redirects, so only
        // other failures need a message here.
        if (error.status !== 401 && error.status !== 403) {
          this.errorMessage.set('Could not load the mood entries. Please try again.');
        }
      }
    });
  }
}
