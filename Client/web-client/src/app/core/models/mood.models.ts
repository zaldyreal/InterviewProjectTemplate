/**
 * Mirrors the backend's MoodRating enum. The numeric values are part of the API contract, so they
 * are stated explicitly rather than left to TypeScript's implicit ordering.
 */
export enum MoodRating {
  NotGoodAtAll = 1,
  ABitMeh = 2,
  PrettyGood = 3,
  FeelingGreat = 4
}

/**
 * A selectable mood. Labels come from the API rather than being duplicated here so the wording
 * required by the brief lives in exactly one place.
 */
export interface MoodOption {
  value: MoodRating;
  label: string;
}

export interface MoodEntry {
  id: number;
  rating: MoodRating;
  ratingLabel: string;
  comment: string | null;
  moodDate: string;
  createdAtUtc: string;
}

export interface TodayMoodStatus {
  hasSubmittedToday: boolean;
  date: string;
  entry: MoodEntry | null;
}

/** An admin-visible entry, which adds the pseudonymous user reference. */
export interface AdminMoodEntry extends MoodEntry {
  userReference: string;
}

export interface PagedResponse<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface CreateMoodEntryRequest {
  rating: MoodRating;
  comment: string | null;
}

export interface AdminLoginRequest {
  username: string;
  password: string;
}

export interface AdminLoginResponse {
  accessToken: string;
  expiresAtUtc: string;
  username: string;
}
