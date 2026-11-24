// The month's note and its limit, without a DOM.
//
// THE SERVER KEEPS AT MOST 1000 UTF-16 code units of the TRIMMED text (NoteService: trimmed, then cut at INoteService.MaxLength, never between the
// two halves of a surrogate pair). The client counts the same way (`text.length` is UTF-16 code units, so an emoji is two) and does not let a
// longer note go: a note the server would cut is a note the person never wrote, and the cut would show only after the save.

/** The longest note: NoteEntity.MaxLength on the server. */
export const NOTE_MAX_LENGTH = 1000;

export interface NoteLimit {
  /** The length of the trimmed text, in UTF-16 code units: what the server counts. */
  length: number;
  /** How many characters too many (0 when it fits). */
  excess: number;
  /** The note is longer than the server keeps: it is not sent. */
  over: boolean;
  /** The text as it is sent: without the white space around it. Empty deletes the note. */
  trimmed: string;
}

/** What a note's text comes to against the limit. A missing text is an empty one. */
export function noteLimit(text: string | null | undefined): NoteLimit {
  // JavaScript's trim and the server's differ in a handful of exotic characters only; the server trims again, so they cannot disagree on length.
  const trimmed = (text ?? '').trim();
  const excess = Math.max(0, trimmed.length - NOTE_MAX_LENGTH);
  return { length: trimmed.length, excess, over: excess > 0, trimmed };
}
