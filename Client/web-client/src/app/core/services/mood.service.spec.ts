import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { environment } from '../../../environments/environment';
import { MoodRating } from '../models/mood.models';
import { MoodService } from './mood.service';

describe('MoodService', () => {
  let service: MoodService;
  let httpMock: HttpTestingController;

  const apiBase = `${environment.apiUrl}/api`;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [MoodService, provideHttpClient(), provideHttpClientTesting()]
    });

    service = TestBed.inject(MoodService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('requests the mood options from the API rather than hardcoding labels', () => {
    service.getOptions().subscribe();

    const request = httpMock.expectOne(`${apiBase}/moods/options`);

    expect(request.request.method).toBe('GET');
    request.flush([]);
  });

  it('sends credentials on public endpoints so the identity cookie is included', () => {
    // Without withCredentials the browser drops the HttpOnly identity cookie on cross-origin calls,
    // giving every request a new identity and defeating the once-per-day rule entirely.
    service.getTodayStatus().subscribe();

    const request = httpMock.expectOne(`${apiBase}/moods/today`);

    expect(request.request.withCredentials).toBeTrue();
    request.flush({ hasSubmittedToday: false, date: '2026-08-06', entry: null });
  });

  it('posts the rating and comment when submitting a mood', () => {
    service.submit({ rating: MoodRating.PrettyGood, comment: 'Good day.' }).subscribe();

    const request = httpMock.expectOne(`${apiBase}/moods`);

    expect(request.request.method).toBe('POST');
    expect(request.request.withCredentials).toBeTrue();
    expect(request.request.body).toEqual({
      rating: MoodRating.PrettyGood,
      comment: 'Good day.'
    });

    request.flush({ id: 1 });
  });

  it('posts a null comment when none was given', () => {
    service.submit({ rating: MoodRating.ABitMeh, comment: null }).subscribe();

    const request = httpMock.expectOne(`${apiBase}/moods`);

    expect(request.request.body.comment).toBeNull();
    request.flush({ id: 1 });
  });

  it('passes paging parameters through to the admin endpoint', () => {
    service.getAllForAdmin(2, 25).subscribe();

    const request = httpMock.expectOne(
      candidate => candidate.url === `${apiBase}/admin/moods`
    );

    expect(request.request.params.get('page')).toBe('2');
    expect(request.request.params.get('pageSize')).toBe('25');

    request.flush({ items: [], totalCount: 0, page: 2, pageSize: 25, totalPages: 0 });
  });

  it('builds URLs from environment.apiUrl so the host is configurable per deployment', () => {
    service.getOptions().subscribe();

    const request = httpMock.expectOne(`${apiBase}/moods/options`);

    expect(request.request.url.startsWith(environment.apiUrl)).toBeTrue();
    request.flush([]);
  });
});
