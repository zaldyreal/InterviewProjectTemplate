import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { environment } from '../../../environments/environment';
import { MoodRating } from '../../core/models/mood.models';
import { MoodTrackerComponent } from './mood-tracker.component';

describe('MoodTrackerComponent', () => {
  let fixture: ComponentFixture<MoodTrackerComponent>;
  let httpMock: HttpTestingController;

  const apiBase = `${environment.apiUrl}/api`;

  const options = [
    { value: MoodRating.NotGoodAtAll, label: 'Not good at all' },
    { value: MoodRating.ABitMeh, label: 'A bit “meh”' },
    { value: MoodRating.PrettyGood, label: 'Pretty good' },
    { value: MoodRating.FeelingGreat, label: 'Feeling great' }
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [MoodTrackerComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    }).compileComponents();

    fixture = TestBed.createComponent(MoodTrackerComponent);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  /** Completes the two requests the component issues on init. */
  function initialise(hasSubmittedToday = false, entry: unknown = null): void {
    fixture.detectChanges();

    httpMock.expectOne(`${apiBase}/moods/options`).flush(options);
    httpMock
      .expectOne(`${apiBase}/moods/today`)
      .flush({ hasSubmittedToday, date: '2026-08-06', entry });

    fixture.detectChanges();
  }

  function text(): string {
    return (fixture.nativeElement as HTMLElement).textContent ?? '';
  }

  it('renders the four mood options with the exact labels from the brief', () => {
    initialise();

    const labels = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll('.option__label')
    ).map(element => element.textContent?.trim());

    expect(labels).toEqual([
      'Not good at all',
      'A bit “meh”',
      'Pretty good',
      'Feeling great'
    ]);
  });

  it('shows the recorded mood instead of the form when today is already logged', () => {
    // Avoids presenting a form that is guaranteed to be rejected on submit.
    initialise(true, {
      id: 4,
      rating: MoodRating.PrettyGood,
      ratingLabel: 'Pretty good',
      comment: 'Shipped it.',
      moodDate: '2026-08-06',
      createdAtUtc: '2026-08-06T09:00:00Z'
    });

    expect(text()).toContain('your mood is recorded');
    expect(text()).toContain('Pretty good');
    expect(text()).toContain('Shipped it.');
    expect((fixture.nativeElement as HTMLElement).querySelector('form')).toBeNull();
  });

  it('does not submit when no mood has been chosen', () => {
    initialise();

    (fixture.nativeElement as HTMLElement).querySelector('form')!
      .dispatchEvent(new Event('submit'));
    fixture.detectChanges();

    httpMock.expectNone(`${apiBase}/moods`);
    expect(text()).toContain('Please choose how you are feeling');
  });

  it('submits the chosen mood and confirms it was recorded', () => {
    initialise();

    const radios = (fixture.nativeElement as HTMLElement)
      .querySelectorAll<HTMLInputElement>('.option__input');

    radios[2].click();
    fixture.detectChanges();

    (fixture.nativeElement as HTMLElement).querySelector('form')!
      .dispatchEvent(new Event('submit'));
    fixture.detectChanges();

    const request = httpMock.expectOne(`${apiBase}/moods`);
    expect(request.request.body.rating).toBe(MoodRating.PrettyGood);

    request.flush({
      id: 1,
      rating: MoodRating.PrettyGood,
      ratingLabel: 'Pretty good',
      comment: null,
      moodDate: '2026-08-06',
      createdAtUtc: '2026-08-06T09:00:00Z'
    });
    fixture.detectChanges();

    expect(text()).toContain('your mood is recorded');
  });

  it('shows the error message from the API when a mood was already recorded today', () => {
    // This is the behaviour the brief asks for by name: a second attempt must show an error.
    initialise();

    (fixture.nativeElement as HTMLElement)
      .querySelectorAll<HTMLInputElement>('.option__input')[0].click();
    fixture.detectChanges();

    (fixture.nativeElement as HTMLElement).querySelector('form')!
      .dispatchEvent(new Event('submit'));
    fixture.detectChanges();

    httpMock.expectOne(`${apiBase}/moods`).flush(
      {
        title: 'Mood already recorded',
        detail: 'You have already recorded your mood today. Please come back tomorrow.'
      },
      { status: 409, statusText: 'Conflict' }
    );
    fixture.detectChanges();

    // The component re-reads today's entry so it can show what was actually stored.
    httpMock.expectOne(`${apiBase}/moods/today`).flush({
      hasSubmittedToday: true,
      date: '2026-08-06',
      entry: {
        id: 9,
        rating: MoodRating.FeelingGreat,
        ratingLabel: 'Feeling great',
        comment: null,
        moodDate: '2026-08-06',
        createdAtUtc: '2026-08-06T08:00:00Z'
      }
    });
    fixture.detectChanges();

    expect(text()).toContain('already recorded your mood today');
    expect(text()).toContain('Feeling great');
  });

  it('reports a connection failure in plain language', () => {
    initialise();

    (fixture.nativeElement as HTMLElement)
      .querySelectorAll<HTMLInputElement>('.option__input')[3].click();
    fixture.detectChanges();

    (fixture.nativeElement as HTMLElement).querySelector('form')!
      .dispatchEvent(new Event('submit'));
    fixture.detectChanges();

    httpMock.expectOne(`${apiBase}/moods`).error(new ProgressEvent('network'), { status: 0 });
    fixture.detectChanges();

    expect(text()).toContain('Could not reach the server');
  });
});
