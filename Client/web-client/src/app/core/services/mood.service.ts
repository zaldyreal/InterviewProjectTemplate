import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../../environments/environment';
import {
  AdminMoodEntry,
  CreateMoodEntryRequest,
  MoodEntry,
  MoodOption,
  PagedResponse,
  TodayMoodStatus
} from '../models/mood.models';

@Injectable({ providedIn: 'root' })
export class MoodService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/api`;

  /**
   * `withCredentials` is essential on the public endpoints: the anonymous identity lives in an
   * HttpOnly cookie, and the browser will not send it cross-origin without this flag. Omitting it
   * would give every request a brand-new identity and silently defeat the once-per-day rule.
   */
  getOptions(): Observable<MoodOption[]> {
    return this.http.get<MoodOption[]>(`${this.baseUrl}/moods/options`, {
      withCredentials: true
    });
  }

  getTodayStatus(): Observable<TodayMoodStatus> {
    return this.http.get<TodayMoodStatus>(`${this.baseUrl}/moods/today`, {
      withCredentials: true
    });
  }

  submit(request: CreateMoodEntryRequest): Observable<MoodEntry> {
    return this.http.post<MoodEntry>(`${this.baseUrl}/moods`, request, {
      withCredentials: true
    });
  }

  getAllForAdmin(page: number, pageSize: number): Observable<PagedResponse<AdminMoodEntry>> {
    return this.http.get<PagedResponse<AdminMoodEntry>>(`${this.baseUrl}/admin/moods`, {
      params: { page, pageSize }
    });
  }
}
